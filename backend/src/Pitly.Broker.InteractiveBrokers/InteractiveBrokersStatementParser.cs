using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Pitly.Core.Models;
using Pitly.Core.Parsing;

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
        var trades = new List<Trade>();
        var dividends = new List<RawDividend>();
        var withholdingTaxes = new List<RawWithholdingTax>();

        var hasTrades = false;
        var hasDividends = false;
        var hasWithholding = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 2) continue;

            var section = fields[0].Trim('"').Trim();

            switch (section)
            {
                case "Trades":
                    hasTrades = true;
                    TryParseTrade(fields, trades);
                    break;
                case "Dividends":
                    hasDividends = true;
                    TryParseDividend(fields, dividends);
                    break;
                case "Withholding Tax":
                    hasWithholding = true;
                    TryParseWithholdingTax(fields, withholdingTaxes);
                    break;
            }
        }

        if (!hasTrades && !hasDividends && !hasWithholding)
            throw new FormatException(
                "File does not appear to be an IB Activity Statement. Please export in CSV format.");

        return new ParsedStatement(trades, dividends, withholdingTaxes);
    }

    private void TryParseTrade(List<string> fields, List<Trade> trades)
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

        trades.Add(new Trade(symbol, currency, dateTime, quantity, price, proceeds,
            Math.Abs(commission), realizedPnl, tradeType));
    }

    private (string Symbol, string Currency, DateTime Date, decimal Amount)? TryParseIncomeRow(
        List<string> fields, string sectionName, bool skipReversals)
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
        if (skipReversals && description.Contains("Reversal", StringComparison.OrdinalIgnoreCase)) return null;

        var symbol = ExtractSymbolFromDescription(description);
        if (symbol == null)
        {
            _logger.LogWarning("Skipping {Section} row: could not extract symbol from '{Description}'", sectionName, description);
            return null;
        }

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

        return (symbol, currency, date, amount);
    }

    private void TryParseDividend(List<string> fields, List<RawDividend> dividends)
    {
        var row = TryParseIncomeRow(fields, "dividend", skipReversals: true);
        if (row is null) return;
        var (symbol, currency, date, amount) = row.Value;
        dividends.Add(new RawDividend(symbol, currency, date, amount));
    }

    private void TryParseWithholdingTax(List<string> fields, List<RawWithholdingTax> taxes)
    {
        var row = TryParseIncomeRow(fields, "withholding tax", skipReversals: false);
        if (row is null) return;
        var (symbol, currency, date, amount) = row.Value;
        taxes.Add(new RawWithholdingTax(symbol, currency, date, Math.Abs(amount)));
    }

    private static string? ExtractSymbolFromDescription(string description)
    {
        var match = SymbolRegex().Match(description);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^(\w+)\s*\(")]
    private static partial Regex SymbolRegex();

    private static string Clean(string value) => value.Trim().Trim('"').Trim();

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

    private static bool TryParseDecimal(string s, out decimal result)
    {
        return decimal.TryParse(s.Replace(",", ""), NumberStyles.Any,
            CultureInfo.InvariantCulture, out result);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }
}
