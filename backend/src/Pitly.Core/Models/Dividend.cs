namespace Pitly.Core.Models;

public record Dividend(
    string Symbol,
    string Currency,
    DateTime Date,
    decimal AmountOriginal,
    decimal WithholdingTaxOriginal,
    decimal AmountPln,
    decimal WithholdingTaxPln,
    decimal ExchangeRate,
    bool RateUnavailable = false);
