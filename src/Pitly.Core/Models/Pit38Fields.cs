namespace Pitly.Core.Models;

public record Pit38Fields(
    int Year,
    decimal C20_Przychody,
    decimal C21_Koszty,
    decimal C22_DochodStrata,
    decimal C23_PodstawaObliczenia,
    decimal C24_Podatek19,
    decimal D25_PrzychodyDywidendy,
    decimal D26_ZryczaltowanyPodatek19,
    decimal E27_PodatekZaplaconyZagranica,
    decimal E28_PodatekDoZaplaty,
    decimal TotalTaxOwed);
