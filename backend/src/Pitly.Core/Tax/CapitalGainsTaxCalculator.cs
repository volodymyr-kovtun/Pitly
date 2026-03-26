using Pitly.Core.Models;
using Pitly.Core.Services;

namespace Pitly.Core.Tax;

public interface ICapitalGainsTaxCalculator
{
    Task<List<TradeResult>> CalculateAsync(
        ParsedStatement statement,
        int targetYear,
        bool assumeGiftedShares = false,
        GiftedLotOverride? giftedLotOverride = null);
}

public class CapitalGainsTaxCalculator : ICapitalGainsTaxCalculator
{
    private readonly INbpExchangeRateService _rateService;

    public CapitalGainsTaxCalculator(INbpExchangeRateService rateService)
    {
        _rateService = rateService;
    }

    public async Task<List<TradeResult>> CalculateAsync(
        ParsedStatement statement,
        int targetYear,
        bool assumeGiftedShares = false,
        GiftedLotOverride? giftedLotOverride = null)
    {
        var results = new List<TradeResult>();
        var buyLots = new Dictionary<string, LinkedList<(decimal Quantity, decimal CostPerSharePln, decimal CommissionPerSharePln, bool IsSynthetic)>>();
        var carryInByKey = (statement.CarryInPositions ?? [])
            .Where(p => p.Year == targetYear)
            .GroupBy(GetLotKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Quantity), StringComparer.OrdinalIgnoreCase);

        // When the user has confirmed these are gifted/bonus shares with no purchase record,
        // pre-populate the FIFO queue with zero-cost synthetic lots from the earliest carry-in
        // per symbol. These sit at the front so FIFO consumes them first and the resulting
        // TradeResult is flagged with HasEstimatedCost = true.
        if (assumeGiftedShares)
        {
            var earliestCarryIns = (statement.CarryInPositions ?? [])
                .GroupBy(GetLotKey, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.MinBy(p => p.Year)!)
                .Where(p => p.Quantity > 0);

            foreach (var carryIn in earliestCarryIns)
            {
                var key = GetLotKey(carryIn);
                if (!buyLots.ContainsKey(key))
                    buyLots[key] = new LinkedList<(decimal, decimal, decimal, bool)>();

                decimal costPerSharePln = 0m;

                if (giftedLotOverride is not null &&
                    carryIn.Symbol.Equals(giftedLotOverride.Symbol, StringComparison.OrdinalIgnoreCase))
                {
                    var grantRate = await _rateService.GetRateAsync(giftedLotOverride.Currency, giftedLotOverride.GrantDate);
                    costPerSharePln = giftedLotOverride.PricePerShare * grantRate;
                }

                buyLots[key].AddFirst((carryIn.Quantity, costPerSharePln, 0m, IsSynthetic: true));
            }
        }

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
                buyLots[lotKey] = new LinkedList<(decimal, decimal, decimal, bool)>();

            if (trade.Type == TradeType.Buy)
            {
                var costPerSharePln = trade.Price * rate;
                var commissionPln = await GetCommissionPlnAsync(trade, rate);
                var commissionPerSharePln = commissionPln / trade.Quantity;
                buyLots[lotKey].AddLast((trade.Quantity, costPerSharePln, commissionPerSharePln, IsSynthetic: false));

                var totalCostPln = trade.Quantity * costPerSharePln + commissionPln;

                if (trade.DateTime.Year == targetYear)
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
                var consumedSynthetic = false;

                if (!buyLots.TryGetValue(lotKey, out var lots) || lots.Count == 0)
                    throw new InvalidOperationException(BuildMissingLotsMessage(trade, targetYear, carryInByKey));

                while (remainingQty > 0 && lots.Count > 0)
                {
                    var lot = lots.First!.Value;
                    var usedQty = Math.Min(remainingQty, lot.Quantity);

                    totalCostPln += usedQty * (lot.CostPerSharePln + lot.CommissionPerSharePln);
                    if (lot.IsSynthetic) consumedSynthetic = true;
                    remainingQty -= usedQty;

                    if (usedQty >= lot.Quantity)
                    {
                        lots.RemoveFirst();
                    }
                    else
                    {
                        lots.First!.Value = (lot.Quantity - usedQty, lot.CostPerSharePln, lot.CommissionPerSharePln, lot.IsSynthetic);
                    }
                }

                if (remainingQty > 0)
                    throw new InvalidOperationException(BuildPartialLotsMessage(
                        trade,
                        trade.Quantity - remainingQty,
                        targetYear,
                        carryInByKey));

                var gainLoss = netProceedsPln - totalCostPln;

                if (trade.DateTime.Year == targetYear)
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
                        GainLossPln: gainLoss,
                        HasEstimatedCost: consumedSynthetic));
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
        Dictionary<string, LinkedList<(decimal Quantity, decimal CostPerSharePln, decimal CommissionPerSharePln, bool IsSynthetic)>> buyLots)
    {
        if (action.Type != CorporateActionType.StockSplit)
            return;

        var factor = action.Factor;
        if (factor <= 0)
            throw new InvalidOperationException(
                $"Unsupported stock split ratio for {action.Symbol} on {action.DateTime:yyyy-MM-dd}.");

        var lotKey = GetLotKey(action);
        if (!buyLots.TryGetValue(lotKey, out var lots) || lots.Count == 0)
            return;

        for (var node = lots.First; node is not null; node = node.Next)
        {
            var lot = node.Value;
            node.Value = (
                Quantity: lot.Quantity * factor,
                CostPerSharePln: lot.CostPerSharePln / factor,
                CommissionPerSharePln: lot.CommissionPerSharePln / factor,
                IsSynthetic: lot.IsSynthetic);
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
