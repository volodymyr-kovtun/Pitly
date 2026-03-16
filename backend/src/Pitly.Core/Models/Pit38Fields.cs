namespace Pitly.Core.Models;

/// <summary>
/// Maps calculated tax values to actual PIT-38(17) form field positions (poz.).
/// Field numbers match the official form published by the Polish Ministry of Finance.
/// </summary>
public record Pit38Fields(
    int Year,

    // ── Section C: Dochody / Straty — art. 30b ust. 1 ustawy ──
    // Row 1 (poz. 20-21): Przychody wykazane w PIT-8C — always 0 for foreign brokers
    // Row 2 (poz. 22-23): Inne przychody — this is where IB income goes
    decimal Poz22Przychody,
    decimal Poz23Koszty,
    // Row 3 (poz. 24-25): Razem = sum of rows 1+2 (equal to 22-23 since row 1 is empty)
    decimal Poz24RazemPrzychody,
    decimal Poz25RazemKoszty,
    // Poz. 26: Dochód (gain), only if proceeds > costs, otherwise 0
    decimal Poz26Dochod,
    // Poz. 27: Strata (loss as positive number), only if costs > proceeds, otherwise 0
    decimal Poz27Strata,

    // ── Section D: Obliczenie zobowiązania podatkowego — art. 30b ust. 1 ustawy ──
    // Poz. 29: Podstawa obliczenia podatku (rounded to full PLN per art. 63 § 1 O.p.)
    decimal Poz29PodstawaObliczenia,
    // Poz. 31: Podatek = poz. 29 × 19%
    decimal Poz31Podatek,
    // Poz. 33: Podatek należny (rounded to full PLN)
    decimal Poz33PodatekNalezny,

    // ── Section G: Podatek do zapłaty / Nadpłata ──
    // Poz. 45: Zryczałtowany podatek 19% od dywidend zagranicznych (art. 30a ust. 1 pkt 1-5)
    decimal Poz45ZryczaltowanyPodatek,
    // Poz. 46: Podatek zapłacony za granicą (capped at poz. 45, per art. 30a ust. 9)
    decimal Poz46PodatekZaplaconyZaGranica,
    // Poz. 47: Różnica = poz. 45 − poz. 46 (rounded to full PLN)
    decimal Poz47Roznica,
    // Poz. 49: PODATEK DO ZAPŁATY = poz. 33 + poz. 47
    decimal Poz49PodatekDoZaplaty,

    // ── Informational (not a PIT-38 field — shown for user reference) ──
    decimal TotalDividendsPln)
{
    public static Pit38Fields FromSummary(TaxSummary summary)
    {
        // Section C — Capital gains
        var przychody = summary.TotalProceedsPln;
        var koszty = summary.TotalCostPln;
        var difference = przychody - koszty;
        var dochod = Math.Round(Math.Max(difference, 0), 2);
        var strata = Math.Round(Math.Max(-difference, 0), 2);

        // Section D — Tax calculation
        var podstawa = RoundToFullPln(dochod);
        var podatek = Math.Round(podstawa * TaxConstants.TaxRate, 2);
        var podatekNalezny = RoundToFullPln(podatek);

        // Section G — Dividends
        var zryczaltowanyPodatek = Math.Round(summary.TotalDividendsPln * TaxConstants.TaxRate, 2);
        var podatekZaGranica = Math.Round(Math.Min(summary.TotalWithholdingPln, zryczaltowanyPodatek), 2);
        var roznica = RoundToFullPln(Math.Max(zryczaltowanyPodatek - podatekZaGranica, 0));

        var podatekDoZaplaty = podatekNalezny + roznica;

        return new Pit38Fields(
            Year: summary.Year,
            Poz22Przychody: przychody,
            Poz23Koszty: koszty,
            Poz24RazemPrzychody: przychody,
            Poz25RazemKoszty: koszty,
            Poz26Dochod: dochod,
            Poz27Strata: strata,
            Poz29PodstawaObliczenia: podstawa,
            Poz31Podatek: podatek,
            Poz33PodatekNalezny: podatekNalezny,
            Poz45ZryczaltowanyPodatek: zryczaltowanyPodatek,
            Poz46PodatekZaplaconyZaGranica: podatekZaGranica,
            Poz47Roznica: roznica,
            Poz49PodatekDoZaplaty: podatekDoZaplaty,
            TotalDividendsPln: summary.TotalDividendsPln);
    }

    /// <summary>
    /// Rounds to full PLN per art. 63 § 1 Ordynacji podatkowej:
    /// amounts below 50 groszy are dropped, 50+ groszy rounded up.
    /// </summary>
    private static decimal RoundToFullPln(decimal value)
        => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}
