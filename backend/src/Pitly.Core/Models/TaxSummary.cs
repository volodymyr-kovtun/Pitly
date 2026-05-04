namespace Pitly.Core.Models;

public record TaxSummary(
    decimal TotalProceedsPln,
    decimal TotalCostPln,
    decimal CapitalGainPln,
    decimal CapitalGainTaxPln,
    decimal TotalDividendsPln,
    decimal TotalWithholdingPln,
    decimal TotalCreditableWithholdingPln,
    decimal DividendTaxOwedPln,
    int Year,
    DateTime TaxableFrom,
    DateTime TaxableTo,
    List<TradeResult> TradeResults,
    List<Dividend> Dividends);
