using Pitly.Core.Models;

namespace Pitly.Core.Tax;

public interface ITaxCalculator
{
    Task<TaxSummary> CalculateAsync(ParsedStatement statement);
}

public class TaxCalculator : ITaxCalculator
{
    private const decimal TAX_RATE = 0.19m;
    private readonly ICapitalGainsTaxCalculator _capitalGainsCalculator;
    private readonly IDividendTaxCalculator _dividendTaxCalculator;

    public TaxCalculator(
        ICapitalGainsTaxCalculator capitalGainsCalculator,
        IDividendTaxCalculator dividendTaxCalculator)
    {
        _capitalGainsCalculator = capitalGainsCalculator;
        _dividendTaxCalculator = dividendTaxCalculator;
    }

    public async Task<TaxSummary> CalculateAsync(ParsedStatement statement)
    {
        var tradeResultsTask = _capitalGainsCalculator.CalculateAsync(statement.Trades);
        var dividendsTask = _dividendTaxCalculator.CalculateAsync(
            statement.Dividends, statement.WithholdingTaxes);
        await Task.WhenAll(tradeResultsTask, dividendsTask);
        var tradeResults = tradeResultsTask.Result;
        var dividends = dividendsTask.Result;

        var sellResults = tradeResults.Where(t => t.Type == TradeType.Sell).ToList();
        var totalProceedsPln = sellResults.Sum(t => t.ProceedsPln);
        var totalCostPln = sellResults.Sum(t => t.CostPln);
        var capitalGain = totalProceedsPln - totalCostPln;
        var capitalGainTax = capitalGain > 0 ? Math.Round(capitalGain * TAX_RATE, 2) : 0;

        var totalDividendsPln = dividends.Sum(d => d.AmountPln);
        var totalWithholdingPln = dividends.Sum(d => d.WithholdingTaxPln);
        var polishDividendTax = Math.Round(totalDividendsPln * TAX_RATE, 2);
        var withholdingCredit = Math.Min(totalWithholdingPln, polishDividendTax);
        var dividendTaxOwed = Math.Max(polishDividendTax - withholdingCredit, 0);

        var year = DetermineYear(statement);

        return new TaxSummary(
            TotalProceedsPln: Math.Round(totalProceedsPln, 2),
            TotalCostPln: Math.Round(totalCostPln, 2),
            CapitalGainPln: Math.Round(capitalGain, 2),
            CapitalGainTaxPln: capitalGainTax,
            TotalDividendsPln: Math.Round(totalDividendsPln, 2),
            TotalWithholdingPln: Math.Round(totalWithholdingPln, 2),
            DividendTaxOwedPln: Math.Round(dividendTaxOwed, 2),
            Year: year,
            TradeResults: tradeResults,
            Dividends: dividends);
    }

    private static int DetermineYear(ParsedStatement statement)
    {
        var allDates = statement.Trades.Select(t => t.DateTime)
            .Concat(statement.Dividends.Select(d => d.Date));

        var grouped = allDates.GroupBy(d => d.Year).OrderByDescending(g => g.Count()).FirstOrDefault();
        return grouped?.Key ?? DateTime.Now.Year;
    }
}
