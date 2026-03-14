namespace Pitly.Core.Models;

public record TaxSummary(
    decimal TotalProceedsPln,
    decimal TotalCostPln,
    decimal CapitalGainPln,
    decimal CapitalGainTaxPln,
    decimal TotalDividendsPln,
    decimal TotalWithholdingPln,
    decimal DividendTaxOwedPln,
    int Year,
    List<TradeResult> TradeResults,
    List<Dividend> Dividends);
