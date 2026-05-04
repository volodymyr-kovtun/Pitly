namespace Pitly.Core.Models;

public record ParsedStatement(
    List<Trade> Trades,
    List<RawDividend> Dividends,
    List<RawWithholdingTax> WithholdingTaxes,
    List<CorporateAction>? CorporateActions = null,
    List<CarryInPosition>? CarryInPositions = null,
    int? StatementYear = null);

public record RawDividend(string Symbol, string Currency, DateTime Date, decimal Amount, string? Isin = null);
public record RawWithholdingTax(string Symbol, string Currency, DateTime Date, decimal Amount, string? Isin = null);

public enum CorporateActionType
{
    StockSplit
}

public record CorporateAction(
    string Symbol,
    DateTime DateTime,
    CorporateActionType Type,
    decimal Numerator,
    decimal Denominator,
    string? Isin = null,
    string? TargetIsin = null)
{
    public decimal Factor => Denominator == 0 ? 0 : Numerator / Denominator;
}

public record CarryInPosition(
    string Symbol,
    decimal Quantity,
    int Year,
    string? Isin = null);
