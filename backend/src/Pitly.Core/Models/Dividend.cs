namespace Pitly.Core.Models;

public record Dividend(
    string Symbol,
    string Currency,
    DateTime Date,
    decimal AmountOriginal,
    decimal WithholdingTaxOriginal,
    decimal AmountPln,
    decimal WithholdingTaxPln,
    decimal CreditableWithholdingTaxPln,
    decimal ExchangeRate,
    string? Isin = null,
    bool RateUnavailable = false);
