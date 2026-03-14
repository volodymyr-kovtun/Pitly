namespace Pitly.Core.Models;

public record ParsedStatement(
    List<Trade> Trades,
    List<RawDividend> Dividends,
    List<RawWithholdingTax> WithholdingTaxes);

public record RawDividend(string Symbol, string Currency, DateTime Date, decimal Amount);
public record RawWithholdingTax(string Symbol, string Currency, DateTime Date, decimal Amount);
