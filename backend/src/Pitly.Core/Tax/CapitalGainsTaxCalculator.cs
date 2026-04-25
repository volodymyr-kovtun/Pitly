using Pitly.Core.Models;
using Pitly.Core.Services;

namespace Pitly.Core.Tax;

public interface ICapitalGainsTaxCalculator
{
    Task<List<TradeResult>> CalculateAsync(ParsedStatement statement, TaxPeriod taxPeriod);
}

public class CapitalGainsTaxCalculator : ICapitalGainsTaxCalculator
{
    private readonly INbpExchangeRateService _rateService;

    public CapitalGainsTaxCalculator(INbpExchangeRateService rateService)
    {
        _rateService = rateService;
    }

    public async Task<List<TradeResult>> CalculateAsync(ParsedStatement statement, TaxPeriod taxPeriod)
    {
        var results = new List<TradeResult>();
        var buyLots = new Dictionary<string, LinkedList<(decimal Quantity, decimal CostPerSharePln, decimal CommissionPerSharePln)>>();
        var carryInByKey = (statement.CarryInPositions ?? [])
            .Where(p => p.Year == taxPeriod.Year)
            .GroupBy(GetLotKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Quantity), StringComparer.OrdinalIgnoreCase);
        var events = statement.Trades
            .Select(trade => new TimelineEvent(trade.DateTime, trade, null))
            .Concat((statement.CorporateActions ?? [])
                .Select(action => new TimelineEvent(action.DateTime, null, action)))
            .OrderBy(e => e.DateTime)
            .ThenBy(e => e.Trade is null ? 0 : 1)
            .ToList();

        foreach (var timelineEvent in events)
        {
            if (timelineEvent.Action is not null)
            {
                ApplyCorporateAction(timelineEvent.Action, buyLots);
                continue;
            }

            var trade = timelineEvent.Trade!;
            var rate = await _rateService.GetRateAsync(trade.Currency, trade.DateTime);
            var lotKey = GetLotKey(trade);

            if (!buyLots.ContainsKey(lotKey))
                buyLots[lotKey] = new LinkedList<(decimal, decimal, decimal)>();

            if (trade.Type == TradeType.Buy)
            {
                var costPerSharePln = trade.Price * rate;
                var commissionPln = await GetCommissionPlnAsync(trade, rate);
                var commissionPerSharePln = commissionPln / trade.Quantity;
                buyLots[lotKey].AddLast((trade.Quantity, costPerSharePln, commissionPerSharePln));

                var totalCostPln = trade.Quantity * costPerSharePln + commissionPln;

                if (taxPeriod.IncludesDate(trade.DateTime))
                {
                    results.Add(new TradeResult(
                        Symbol: trade.Symbol,
                        DateTime: trade.DateTime,
                        Type: TradeType.Buy,
                        Quantity: trade.Quantity,
                        PriceOriginal: trade.Price,
                        ProceedsOriginal: 0,
                        CommissionOriginal: trade.Commission,
                        Currency: trade.Currency,
                        ExchangeRate: rate,
                        ProceedsPln: 0,
                        CostPln: totalCostPln,
                        GainLossPln: 0));
                }
            }
            else
            {
                var proceedsPln = trade.Proceeds * rate;
                var sellCommissionPln = await GetCommissionPlnAsync(trade, rate);
                var netProceedsPln = proceedsPln - sellCommissionPln;

                decimal totalCostPln = 0;
                var remainingQty = trade.Quantity;

                if (!buyLots.TryGetValue(lotKey, out var lots) || lots.Count == 0)
                    throw new InvalidOperationException(BuildMissingLotsMessage(trade, taxPeriod.Year, carryInByKey));

                while (remainingQty > 0 && lots.Count > 0)
                {
                    var lot = lots.First!.Value;
                    var usedQty = Math.Min(remainingQty, lot.Quantity);

                    totalCostPln += usedQty * (lot.CostPerSharePln + lot.CommissionPerSharePln);
                    remainingQty -= usedQty;

                    if (usedQty >= lot.Quantity)
                    {
                        lots.RemoveFirst();
                    }
                    else
                    {
                        lots.First!.Value = (lot.Quantity - usedQty, lot.CostPerSharePln, lot.CommissionPerSharePln);
                    }
                }

                if (remainingQty > 0)
                    throw new InvalidOperationException(BuildPartialLotsMessage(
                        trade,
                        trade.Quantity - remainingQty,
                        taxPeriod.Year,
                        carryInByKey));

                var gainLoss = netProceedsPln - totalCostPln;

                if (taxPeriod.IncludesDate(trade.DateTime))
                {
                    results.Add(new TradeResult(
                        Symbol: trade.Symbol,
                        DateTime: trade.DateTime,
                        Type: TradeType.Sell,
                        Quantity: trade.Quantity,
                        PriceOriginal: trade.Price,
                        ProceedsOriginal: trade.Proceeds,
                        CommissionOriginal: trade.Commission,
                        Currency: trade.Currency,
                        ExchangeRate: rate,
                        ProceedsPln: netProceedsPln,
                        CostPln: totalCostPln,
                        GainLossPln: gainLoss));
                }
            }
        }

        return results;
    }

    private async Task<decimal> GetCommissionPlnAsync(Trade trade, decimal tradeRate)
    {
        if (trade.Commission == 0)
            return 0;

        var commissionRate = trade.CommissionCurrency.Equals(trade.Currency, StringComparison.OrdinalIgnoreCase)
            ? tradeRate
            : await _rateService.GetRateAsync(trade.CommissionCurrency, trade.DateTime);

        return trade.Commission * commissionRate;
    }

    private static void ApplyCorporateAction(
        CorporateAction action,
        Dictionary<string, LinkedList<(decimal Quantity, decimal CostPerSharePln, decimal CommissionPerSharePln)>> buyLots)
    {
        if (action.Type != CorporateActionType.StockSplit)
            return;

        var factor = action.Factor;
        if (factor <= 0)
            throw new InvalidOperationException(
                $"Unsupported stock split ratio for {action.Symbol} on {action.DateTime:yyyy-MM-dd}.");

        // A split row may name the ISIN before the split (`Isin`) and a different one after it
        // (`TargetIsin`). Trades may have landed under either ISIN or under the bare symbol — try
        // each in turn so the rescale finds the lots wherever they live.
        var sourceKey = GetLotKey(action);
        var targetKey = string.IsNullOrWhiteSpace(action.TargetIsin) ? sourceKey : action.TargetIsin;

        string? actualKey = null;
        LinkedList<(decimal Quantity, decimal CostPerSharePln, decimal CommissionPerSharePln)>? lots = null;
        foreach (var candidate in new[] { sourceKey, targetKey, action.Symbol })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (buyLots.TryGetValue(candidate, out lots) && lots.Count > 0)
            {
                actualKey = candidate;
                break;
            }
        }
        if (actualKey is null || lots is null)
            return;

        for (var node = lots.First; node is not null; node = node.Next)
        {
            var lot = node.Value;
            node.Value = (
                Quantity: lot.Quantity * factor,
                CostPerSharePln: lot.CostPerSharePln / factor,
                CommissionPerSharePln: lot.CommissionPerSharePln / factor);
        }

        if (!string.Equals(actualKey, targetKey, StringComparison.OrdinalIgnoreCase))
        {
            // Trades on either side of the rename may resolve their lot key to either ISIN
            // depending on which "Financial Instrument Information" line the parser saw last.
            // Expose the same lot list under both keys so a sell finds it regardless.
            if (buyLots.TryGetValue(targetKey, out var existing) && !ReferenceEquals(existing, lots))
            {
                foreach (var lot in lots)
                    existing.AddLast(lot);
                buyLots[actualKey] = existing;
            }
            else
            {
                buyLots[targetKey] = lots;
            }
        }
    }

    private static string BuildMissingLotsMessage(
        Trade trade,
        int targetYear,
        IReadOnlyDictionary<string, decimal> carryInByKey)
    {
        if (carryInByKey.TryGetValue(GetLotKey(trade), out var carryInQuantity) && carryInQuantity > 0)
        {
            return
                $"Cannot sell {trade.Quantity} shares of {trade.Symbol} on {trade.DateTime:yyyy-MM-dd}: " +
                $"this sale depends on {carryInQuantity} shares carried into {targetYear} from earlier statements. " +
                "Upload prior-year CSVs so Pitly can reconstruct the original FIFO buy lots.";
        }

        return
            $"Cannot sell {trade.Quantity} shares of {trade.Symbol} on {trade.DateTime:yyyy-MM-dd}: no buy lots available. " +
            "Upload earlier statements so Pitly can reconstruct the original FIFO buy lots.";
    }

    private static string BuildPartialLotsMessage(
        Trade trade,
        decimal availableQuantity,
        int targetYear,
        IReadOnlyDictionary<string, decimal> carryInByKey)
    {
        if (carryInByKey.TryGetValue(GetLotKey(trade), out var carryInQuantity) && carryInQuantity > 0)
        {
            return
                $"Cannot sell {trade.Quantity} shares of {trade.Symbol} on {trade.DateTime:yyyy-MM-dd}: " +
                $"only {availableQuantity} shares were reconstructed from uploaded statements, and this position carried {carryInQuantity} shares into {targetYear}. " +
                "Upload prior-year CSVs so Pitly can reconstruct the missing FIFO buy lots.";
        }

        return
            $"Cannot sell {trade.Quantity} shares of {trade.Symbol} on {trade.DateTime:yyyy-MM-dd}: " +
            $"only {availableQuantity} shares available in buy lots. " +
            "Upload earlier statements so Pitly can reconstruct the missing FIFO buy lots.";
    }

    private static string GetLotKey(Trade trade) => string.IsNullOrWhiteSpace(trade.Isin)
        ? trade.Symbol
        : trade.Isin;

    private static string GetLotKey(CorporateAction action) => string.IsNullOrWhiteSpace(action.Isin)
        ? action.Symbol
        : action.Isin;

    private static string GetLotKey(CarryInPosition position) => string.IsNullOrWhiteSpace(position.Isin)
        ? position.Symbol
        : position.Isin;

    private sealed record TimelineEvent(DateTime DateTime, Trade? Trade, CorporateAction? Action);
}
