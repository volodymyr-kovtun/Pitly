using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Pitly.Core.Models;
using Pitly.Core.Parsing;
using static Pitly.Core.Parsing.CsvHelpers;

namespace Pitly.Broker.InteractiveBrokers;

public partial class InteractiveBrokersStatementParser : IStatementParser
{
    private readonly ILogger<InteractiveBrokersStatementParser> _logger;

    public InteractiveBrokersStatementParser(ILogger<InteractiveBrokersStatementParser> logger)
    {
        _logger = logger;
    }

    public ParsedStatement Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new FormatException("File is empty.");
        }

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var tradeRows = new List<List<string>>();
        var dividendRows = new List<List<string>>();
        var withholdingRows = new List<List<string>>();
        var corporateActionRows = new List<List<string>>();
        var carryInRows = new List<List<string>>();
        var grantRows = new List<List<string>>();
        var symbolToIsin = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trades = new List<Trade>();
        var dividends = new List<RawDividend>();
        var withholdingTaxes = new List<RawWithholdingTax>();
        var corporateActions = new List<CorporateAction>();
        var carryInPositions = new List<CarryInPosition>();
        var unsupportedCorporateActions = new List<string>();

        var hasTrades = false;
        var hasDividends = false;
        var hasWithholding = false;
        int? statementYear = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 2) continue;

            var section = fields[0].Trim('"').Trim();

            switch (section)
            {
                case "Statement":
                    TryParseStatementYear(fields, ref statementYear);
                    break;
                case "Trades":
                    hasTrades = true;
                    tradeRows.Add(fields);
                    break;
                case "Dividends":
                    hasDividends = true;
                    dividendRows.Add(fields);
                    break;
                case "Withholding Tax":
                    hasWithholding = true;
                    withholdingRows.Add(fields);
                    break;
                case "Financial Instrument Information":
                    TryParseFinancialInstrument(fields, symbolToIsin);
                    break;
                case "Corporate Actions":
                    corporateActionRows.Add(fields);
                    break;
                case "Mark-to-Market Performance Summary":
                    carryInRows.Add(fields);
                    break;
                case "Grant Activity":
                    grantRows.Add(fields);
                    break;
            }
        }

        if (!hasTrades && !hasDividends && !hasWithholding && grantRows.Count == 0)
            throw new FormatException(
                "File does not appear to be an IB Activity Statement. Please export in CSV format.");

        foreach (var fields in tradeRows)
            TryParseTrade(fields, trades, symbolToIsin);

        foreach (var fields in grantRows)
            TryParseGrant(fields, trades, symbolToIsin);

        foreach (var fields in dividendRows)
            TryParseDividend(fields, dividends);

        foreach (var fields in withholdingRows)
            TryParseWithholdingTax(fields, withholdingTaxes);

        statementYear ??= InferStatementYear(trades, dividends, withholdingTaxes);

        foreach (var fields in corporateActionRows)
            TryParseCorporateAction(fields, corporateActions, unsupportedCorporateActions, symbolToIsin);

        foreach (var fields in carryInRows)
            TryParseCarryInPosition(fields, carryInPositions, symbolToIsin, statementYear);

        if (unsupportedCorporateActions.Count > 0)
        {
            throw new FormatException(
                "This Interactive Brokers statement contains unsupported stock corporate actions. " +
                $"Pitly currently supports stock splits only. First unsupported row: '{unsupportedCorporateActions[0]}'.");
        }

        return new ParsedStatement(
            trades,
            NetDividends(dividends),
            NetWithholdingTaxes(withholdingTaxes),
            corporateActions,
            carryInPositions,
            statementYear);
    }

    private void TryParseTrade(
        List<string> fields,
        List<Trade> trades,
        IReadOnlyDictionary<string, string> symbolToIsin)
    {
        // CSV: Trades,Data,DataDiscriminator,AssetCategory,Currency,Symbol,DateTime,Qty,Price,CPrice,Proceeds,Comm,Basis,RealizedPnL,MTM,Code
        // Index: 0     1    2                3             4        5      6        7   8     9      10       11   12    13          14  15
        if (fields.Count < 2) return;

        var discriminator = Clean(fields[1]);
        if (discriminator != "Data") return;

        if (fields.Count < 16)
        {
            _logger.LogWarning("Skipping trade data row: expected at least 16 fields but found {Count}", fields.Count);
            return;
        }

        var dataDiscriminator = Clean(fields[2]);
        // Real IB exports use "Order" (or "Lot"), sample files may use "Data"
        // Skip header/subtotal/total rows but accept any data discriminator
        if (dataDiscriminator is "Header" or "SubTotal" or "Total") return;

        var assetCategory = Clean(fields[3]);
        if (assetCategory != "Stocks") return;

        var currency = Clean(fields[4]);
        var symbol = Clean(fields[5]);
        var dateTimeStr = Clean(fields[6]);
        var quantityStr = Clean(fields[7]);
        var priceStr = Clean(fields[8]);
        var proceedsStr = Clean(fields[10]);
        var commissionStr = Clean(fields[11]);
        var realizedPnlStr = Clean(fields[13]);

        if (!TryParseDateTime(dateTimeStr, out var dateTime))
        {
            _logger.LogWarning("Skipping trade row for {Symbol}: could not parse date '{DateStr}'", symbol, dateTimeStr);
            return;
        }
        if (!TryParseDecimal(quantityStr, out var quantity))
        {
            _logger.LogWarning("Skipping trade row for {Symbol} on {Date}: could not parse quantity '{QuantityStr}'", symbol, dateTimeStr, quantityStr);
            return;
        }
        if (!TryParseDecimal(priceStr, out var price))
        {
            _logger.LogWarning("Skipping trade row for {Symbol} on {Date}: could not parse price '{PriceStr}'", symbol, dateTimeStr, priceStr);
            return;
        }
        if (!TryParseDecimal(proceedsStr, out var proceeds))
        {
            _logger.LogWarning("Skipping trade row for {Symbol} on {Date}: could not parse proceeds '{ProceedsStr}'", symbol, dateTimeStr, proceedsStr);
            return;
        }
        if (!TryParseDecimal(commissionStr, out var commission))
        {
            _logger.LogWarning("Skipping trade row for {Symbol} on {Date}: could not parse commission '{CommissionStr}'", symbol, dateTimeStr, commissionStr);
            return;
        }
        TryParseDecimal(realizedPnlStr, out var realizedPnl);

        var tradeType = quantity > 0 ? TradeType.Buy : TradeType.Sell;
        quantity = Math.Abs(quantity);
        proceeds = Math.Abs(proceeds);
        symbolToIsin.TryGetValue(symbol, out var isin);

        trades.Add(new Trade(symbol, currency, dateTime, quantity, price, proceeds,
            Math.Abs(commission), currency, realizedPnl, tradeType, isin));
    }

    private void TryParseGrant(
        List<string> fields,
        List<Trade> trades,
        IReadOnlyDictionary<string, string> symbolToIsin)
    {
        // CSV: Grant Activity,Data,Symbol,ReportDate,Description,AwardDate,VestingDate,Quantity,Price,Value
        // Index: 0             1    2      3          4           5         6           7        8     9
        if (fields.Count < 2) return;

        var discriminator = Clean(fields[1]);
        if (discriminator != "Data") return;

        if (fields.Count < 10)
        {
            _logger.LogWarning("Skipping grant activity row: expected at least 10 fields but found {Count}", fields.Count);
            return;
        }

        var symbol = Clean(fields[2]);
        if (symbol.Equals("Total", StringComparison.OrdinalIgnoreCase)) return;

        var awardDateStr = Clean(fields[5]);
        var quantityStr = Clean(fields[7]);
        var priceStr = Clean(fields[8]);
        var valueStr = Clean(fields[9]);

        if (!TryParseDate(awardDateStr, out var awardDate))
        {
            _logger.LogWarning("Skipping grant activity row for {Symbol}: could not parse award date '{DateStr}'", symbol, awardDateStr);
            return;
        }
        if (!TryParseDecimal(quantityStr, out var quantity) || quantity <= 0)
        {
            _logger.LogWarning("Skipping grant activity row for {Symbol}: could not parse quantity '{QuantityStr}'", symbol, quantityStr);
            return;
        }
        if (!TryParseDecimal(priceStr, out var price))
        {
            _logger.LogWarning("Skipping grant activity row for {Symbol}: could not parse price '{PriceStr}'", symbol, priceStr);
            return;
        }
        if (!TryParseDecimal(valueStr, out var value))
        {
            value = quantity * price;
        }

        symbolToIsin.TryGetValue(symbol, out var isin);

        trades.Add(new Trade(symbol, "USD", awardDate, quantity, price, value,
            0, "USD", 0, TradeType.Buy, isin));
    }

    private (string Symbol, string? Isin, string Currency, DateTime Date, decimal Amount)? TryParseIncomeRow(
        List<string> fields, string sectionName)
    {
        if (fields.Count < 2) return null;

        var discriminator = Clean(fields[1]);
        if (discriminator != "Data") return null;

        if (fields.Count < 6)
        {
            _logger.LogWarning("Skipping {Section} data row: expected at least 6 fields but found {Count}", sectionName, fields.Count);
            return null;
        }

        var currency = Clean(fields[2]);
        var dateStr = Clean(fields[3]);
        var description = Clean(fields[4]);
        var amountStr = Clean(fields[5]);

        if (description.Contains("Total", StringComparison.OrdinalIgnoreCase)) return null;

        var instrument = ExtractInstrumentFromDescription(description);
        if (instrument is null)
        {
            _logger.LogWarning("Skipping {Section} row: could not extract symbol from '{Description}'", sectionName, description);
            return null;
        }

        var (symbol, isin) = instrument.Value;

        if (!TryParseDate(dateStr, out var date))
        {
            _logger.LogWarning("Skipping {Section} row for {Symbol}: could not parse date '{DateStr}'", sectionName, symbol, dateStr);
            return null;
        }
        if (!TryParseDecimal(amountStr, out var amount))
        {
            _logger.LogWarning("Skipping {Section} row for {Symbol} on {Date}: could not parse amount '{AmountStr}'", sectionName, symbol, dateStr, amountStr);
            return null;
        }

        return (symbol, isin, currency, date, amount);
    }

    private void TryParseDividend(List<string> fields, List<RawDividend> dividends)
    {
        var row = TryParseIncomeRow(fields, "dividend");
        if (row is null) return;
        var (symbol, isin, currency, date, amount) = row.Value;
        dividends.Add(new RawDividend(symbol, currency, date, amount, isin));
    }

    private void TryParseWithholdingTax(List<string> fields, List<RawWithholdingTax> taxes)
    {
        var row = TryParseIncomeRow(fields, "withholding tax");
        if (row is null) return;
        var (symbol, isin, currency, date, amount) = row.Value;
        taxes.Add(new RawWithholdingTax(symbol, currency, date, amount, isin));
    }

    private static List<RawDividend> NetDividends(List<RawDividend> dividends)
    {
        return dividends
            .GroupBy(d => (d.Symbol, d.Isin, d.Currency, d.Date))
            .Select(g => new RawDividend(g.Key.Symbol, g.Key.Currency, g.Key.Date, g.Sum(d => d.Amount), g.Key.Isin))
            .Where(d => d.Amount > 0)
            .ToList();
    }

    private static List<RawWithholdingTax> NetWithholdingTaxes(List<RawWithholdingTax> taxes)
    {
        return taxes
            .GroupBy(t => (t.Symbol, t.Isin, t.Currency, t.Date))
            .Select(g => new RawWithholdingTax(g.Key.Symbol, g.Key.Currency, g.Key.Date, Math.Abs(g.Sum(t => t.Amount)), g.Key.Isin))
            .Where(t => t.Amount > 0)
            .ToList();
    }

    private static (string Symbol, string? Isin)? ExtractInstrumentFromDescription(string description)
    {
        var match = InstrumentDescriptionRegex().Match(description);
        if (!match.Success)
            return null;

        var symbol = match.Groups["symbol"].Value;
        var isin = match.Groups["isin"].Success ? match.Groups["isin"].Value : null;
        return (symbol, isin);
    }

    [GeneratedRegex(@"^\s*(?<symbol>\w+)\s*\((?<isin>[^)]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex InstrumentDescriptionRegex();

    [GeneratedRegex(@"^\s*(?<symbol>\w+)\s*\((?<isin>[^)]+)\)\s+(?:Reverse\s+)?Split\s+(?<num>\d+(?:\.\d+)?)\s+for\s+(?<den>\d+(?:\.\d+)?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StockSplitRegex();

    private void TryParseStatementYear(List<string> fields, ref int? statementYear)
    {
        if (fields.Count < 4 || Clean(fields[1]) != "Data" || Clean(fields[2]) != "Period")
            return;

        var period = Clean(fields[3]);
        var parts = period.Split(" - ", 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return;

        if (DateTime.TryParseExact(parts[1], "MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
            statementYear = endDate.Year;
    }

    private void TryParseFinancialInstrument(List<string> fields, Dictionary<string, string> symbolToIsin)
    {
        if (fields.Count < 7 || Clean(fields[1]) != "Data" || Clean(fields[2]) != "Stocks")
            return;

        var symbolField = Clean(fields[3]);
        var isin = Clean(fields[6]);
        if (string.IsNullOrWhiteSpace(symbolField) || string.IsNullOrWhiteSpace(isin))
            return;

        // IB lists every alias the instrument has held in this period in one comma-separated cell
        // (e.g. "FRC, FRCB" or "BOIL.OLD, BOIL" after a ticker change). Register each alias so the
        // ISIN attaches to trades regardless of which name they reference.
        foreach (var part in symbolField.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            symbolToIsin[part] = isin;
    }

    private void TryParseCorporateAction(
        List<string> fields,
        List<CorporateAction> corporateActions,
        List<string> unsupportedCorporateActions,
        IReadOnlyDictionary<string, string> symbolToIsin)
    {
        if (fields.Count < 7 || Clean(fields[1]) != "Data" || Clean(fields[2]) != "Stocks")
            return;

        var description = Clean(fields[6]);
        if (string.IsNullOrWhiteSpace(description) || description.Contains("Total", StringComparison.OrdinalIgnoreCase))
            return;

        var match = StockSplitRegex().Match(description);
        if (!match.Success)
        {
            unsupportedCorporateActions.Add(description);
            return;
        }

        // IB emits a split as two rows: a positive-quantity credit of the new shares and a
        // negative-quantity debit of the old shares (often with the .OLD alias). Both rows match
        // the split regex, so without skipping the debit we'd apply the split factor twice.
        if (fields.Count > 7 && TryParseDecimal(Clean(fields[7]), out var quantity) && quantity <= 0)
            return;

        var dateTimeStr = Clean(fields[5]);
        if (!TryParseDateTime(dateTimeStr, out var dateTime))
        {
            _logger.LogWarning("Skipping corporate action row: could not parse date '{DateStr}'", dateTimeStr);
            return;
        }

        if (!TryParseDecimal(match.Groups["num"].Value, out var numerator) ||
            !TryParseDecimal(match.Groups["den"].Value, out var denominator))
        {
            unsupportedCorporateActions.Add(description);
            return;
        }

        var symbol = match.Groups["symbol"].Value;
        var isin = match.Groups["isin"].Value;
        if (string.IsNullOrWhiteSpace(isin) && symbolToIsin.TryGetValue(symbol, out var mappedIsin))
            isin = mappedIsin;

        corporateActions.Add(new CorporateAction(
            Symbol: symbol,
            DateTime: dateTime,
            Type: CorporateActionType.StockSplit,
            Numerator: numerator,
            Denominator: denominator,
            Isin: string.IsNullOrWhiteSpace(isin) ? null : isin));
    }

    private void TryParseCarryInPosition(
        List<string> fields,
        List<CarryInPosition> carryInPositions,
        IReadOnlyDictionary<string, string> symbolToIsin,
        int? statementYear)
    {
        if (statementYear is null)
            return;

        if (fields.Count < 5 || Clean(fields[1]) != "Data" || Clean(fields[2]) != "Stocks")
            return;

        var symbol = Clean(fields[3]);
        var quantityStr = Clean(fields[4]);
        if (!TryParseDecimal(quantityStr, out var quantity) || quantity <= 0)
            return;

        symbolToIsin.TryGetValue(symbol, out var isin);
        carryInPositions.Add(new CarryInPosition(symbol, quantity, statementYear.Value, isin));
    }

    private static int InferStatementYear(
        IEnumerable<Trade> trades,
        IEnumerable<RawDividend> dividends,
        IEnumerable<RawWithholdingTax> withholdingTaxes)
    {
        var years = trades.Select(t => t.DateTime.Year)
            .Concat(dividends.Select(d => d.Date.Year))
            .Concat(withholdingTaxes.Select(t => t.Date.Year))
            .ToList();

        if (years.Count == 0)
        {
            throw new FormatException(
                "The statement contains no trades or dividends. Please check that the uploaded file is a valid broker export.");
        }

        return years.Max();
    }

    private static bool TryParseDateTime(string s, out DateTime result)
    {
        var formats = new[] { "yyyy-MM-dd, HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd" };
        return DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out result);
    }

    private static bool TryParseDate(string s, out DateTime result)
    {
        return DateTime.TryParseExact(s.Trim(), new[] { "yyyy-MM-dd", "MM/dd/yyyy" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

}
