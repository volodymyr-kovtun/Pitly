namespace Pitly.Core.Models;

public record Pit38Fields(
    int Year,
    decimal C20Przychody,
    decimal C21Koszty,
    decimal C22DochodStrata,
    decimal C23PodstawaObliczenia,
    decimal C24Podatek19,
    decimal D25PrzychodyDywidendy,
    decimal D26ZryczaltowanyPodatek19,
    decimal E27PodatekZaplaconyZagranica,
    decimal E28PodatekDoZaplaty,
    decimal TotalTaxOwed)
{
    private const decimal TAX_RATE = 0.19m;

    public static Pit38Fields FromSummary(TaxSummary summary)
    {
        var c22 = summary.CapitalGainPln;
        var c23 = c22 > 0 ? c22 : 0;
        var c24 = summary.CapitalGainTaxPln;

        var d25 = summary.TotalDividendsPln;
        var d26 = Math.Round(d25 * TAX_RATE, 2);

        var e27 = Math.Round(Math.Min(summary.TotalWithholdingPln, d26), 2);
        var e28 = Math.Round(Math.Max(d26 - e27, 0), 2);

        var totalTax = c24 + e28;

        return new Pit38Fields(
            Year: summary.Year,
            C20Przychody: summary.TotalProceedsPln,
            C21Koszty: summary.TotalCostPln,
            C22DochodStrata: c22,
            C23PodstawaObliczenia: c23,
            C24Podatek19: c24,
            D25PrzychodyDywidendy: d25,
            D26ZryczaltowanyPodatek19: d26,
            E27PodatekZaplaconyZagranica: e27,
            E28PodatekDoZaplaty: e28,
            TotalTaxOwed: totalTax);
    }
}
