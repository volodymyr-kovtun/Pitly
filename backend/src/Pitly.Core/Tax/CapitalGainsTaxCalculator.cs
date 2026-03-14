using Pitly.Core.Models;
using Pitly.Core.Services;

namespace Pitly.Core.Tax;

public interface ICapitalGainsTaxCalculator
{
    Task<List<TradeResult>> CalculateAsync(List<Trade> trades);
}

public class CapitalGainsTaxCalculator : ICapitalGainsTaxCalculator
{
    private readonly INbpExchangeRateService _rateService;

    public CapitalGainsTaxCalculator(INbpExchangeRateService rateService)
    {
        _rateService = rateService;
    }

    public async Task<List<TradeResult>> CalculateAsync(List<Trade> trades)
    {
        var results = new List<TradeResult>();
        var buyLots = new Dictionary<string, LinkedList<(decimal Quantity, decimal CostPerSharePln, decimal CommissionPerSharePln)>>();

        var sorted = trades.OrderBy(t => t.DateTime).ToList();

        foreach (var trade in sorted)
        {
            decimal rate;
            bool rateUnavailable = false;
            try
            {
                rate = await _rateService.GetRateAsync(trade.Currency, trade.DateTime);
            }
            catch
            {
                rate = 0;
                rateUnavailable = true;
            }

            if (!buyLots.ContainsKey(trade.Symbol))
                buyLots[trade.Symbol] = new LinkedList<(decimal, decimal, decimal)>();

            if (trade.Type == TradeType.Buy)
            {
                var costPerSharePln = trade.Price * rate;
                var commissionPerSharePln = (trade.Commission / trade.Quantity) * rate;
                buyLots[trade.Symbol].AddLast((trade.Quantity, costPerSharePln, commissionPerSharePln));

                var totalCostPln = trade.Quantity * costPerSharePln + trade.Commission * rate;

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
                    GainLossPln: 0,
                    RateUnavailable: rateUnavailable));
            }
            else
            {
                var proceedsPln = trade.Proceeds * rate;
                var sellCommissionPln = trade.Commission * rate;
                var netProceedsPln = proceedsPln - sellCommissionPln;

                decimal totalCostPln = 0;
                var remainingQty = trade.Quantity;
                var lots = buyLots[trade.Symbol];

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

                var gainLoss = netProceedsPln - totalCostPln;

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
                    RateUnavailable: rateUnavailable));
            }
        }

        return results;
    }
}
