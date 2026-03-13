using Pitly.Core.Models;
using Pitly.Core.Parsing;
using Pitly.Core.Services;

namespace Pitly.Core.Tax;

public class TaxCalculator
{
    private readonly CapitalGainsTaxEngine _capitalGainsEngine;
    private readonly DividendTaxEngine _dividendEngine;

    public TaxCalculator(INbpExchangeRateService rateService)
    {
        _capitalGainsEngine = new CapitalGainsTaxEngine(rateService);
        _dividendEngine = new DividendTaxEngine(rateService);
    }

    public async Task<TaxSummary> CalculateAsync(ParsedStatement statement)
    {
        var tradeResults = await _capitalGainsEngine.CalculateAsync(statement.Trades);
        var dividends = await _dividendEngine.CalculateAsync(
            statement.Dividends, statement.WithholdingTaxes);

        var sellResults = tradeResults.Where(t => t.Type == TradeType.Sell).ToList();
        var totalProceedsPln = sellResults.Sum(t => t.ProceedsPln);
        var totalCostPln = sellResults.Sum(t => t.CostPln);
        var capitalGain = totalProceedsPln - totalCostPln;
        var capitalGainTax = capitalGain > 0 ? Math.Round(capitalGain * 0.19m, 2) : 0;

        var totalDividendsPln = dividends.Sum(d => d.AmountPln);
        var totalWithholdingPln = dividends.Sum(d => d.WithholdingTaxPln);
        var polishDividendTax = Math.Round(totalDividendsPln * 0.19m, 2);
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

    public static Pit38Fields BuildPit38(TaxSummary summary)
    {
        var c22 = summary.CapitalGainPln;
        var c23 = c22 > 0 ? c22 : 0;
        var c24 = summary.CapitalGainTaxPln;

        var d25 = summary.TotalDividendsPln;
        var d26 = Math.Round(d25 * 0.19m, 2);

        var e27 = Math.Round(Math.Min(summary.TotalWithholdingPln, d26), 2);
        var e28 = Math.Round(Math.Max(d26 - e27, 0), 2);

        var totalTax = c24 + e28;

        return new Pit38Fields(
            Year: summary.Year,
            C20_Przychody: summary.TotalProceedsPln,
            C21_Koszty: summary.TotalCostPln,
            C22_DochodStrata: c22,
            C23_PodstawaObliczenia: c23,
            C24_Podatek19: c24,
            D25_PrzychodyDywidendy: d25,
            D26_ZryczaltowanyPodatek19: d26,
            E27_PodatekZaplaconyZagranica: e27,
            E28_PodatekDoZaplaty: e28,
            TotalTaxOwed: totalTax);
    }

    private static int DetermineYear(ParsedStatement statement)
    {
        var allDates = statement.Trades.Select(t => t.DateTime)
            .Concat(statement.Dividends.Select(d => d.Date))
            .ToList();

        if (allDates.Count == 0) return DateTime.Now.Year;
        return allDates.GroupBy(d => d.Year).OrderByDescending(g => g.Count()).First().Key;
    }
}
