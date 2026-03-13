using System.Globalization;
using System.Text.RegularExpressions;
using Pitly.Core.Models;

namespace Pitly.Core.Parsing;

public record ParsedStatement(
    List<Trade> Trades,
    List<RawDividend> Dividends,
    List<RawWithholdingTax> WithholdingTaxes);

public record RawDividend(string Symbol, string Currency, DateTime Date, decimal Amount);
public record RawWithholdingTax(string Symbol, string Currency, DateTime Date, decimal Amount);

public static partial class IbActivityParser
{
    public static ParsedStatement Parse(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
            throw new FormatException("File is empty.");

        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
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

    private static void TryParseTrade(List<string> fields, List<Trade> trades)
    {
        // CSV: Trades,Data,DataDiscriminator,AssetCategory,Currency,Symbol,DateTime,Qty,Price,CPrice,Proceeds,Comm,Basis,RealizedPnL,MTM,Code
        // Index: 0     1    2                3             4        5      6        7   8     9      10       11   12    13          14  15
        if (fields.Count < 16) return;

        var discriminator = Clean(fields[1]);
        if (discriminator != "Data") return;

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

        if (!TryParseDateTime(dateTimeStr, out var dateTime)) return;
        if (!TryParseDecimal(quantityStr, out var quantity)) return;
        if (!TryParseDecimal(priceStr, out var price)) return;
        if (!TryParseDecimal(proceedsStr, out var proceeds)) return;
        if (!TryParseDecimal(commissionStr, out var commission)) return;
        TryParseDecimal(realizedPnlStr, out var realizedPnl);

        var tradeType = quantity > 0 ? TradeType.Buy : TradeType.Sell;
        quantity = Math.Abs(quantity);
        proceeds = Math.Abs(proceeds);

        trades.Add(new Trade(symbol, currency, dateTime, quantity, price, proceeds,
            Math.Abs(commission), realizedPnl, tradeType));
    }

    private static void TryParseDividend(List<string> fields, List<RawDividend> dividends)
    {
        if (fields.Count < 5) return;

        var discriminator = Clean(fields[1]);
        if (discriminator != "Data") return;

        var currency = Clean(fields[2]);
        var dateStr = Clean(fields[3]);
        var description = Clean(fields[4]);
        var amountStr = Clean(fields[5]);

        if (description.Contains("Total", StringComparison.OrdinalIgnoreCase)) return;
        if (description.Contains("Reversal", StringComparison.OrdinalIgnoreCase)) return;

        var symbol = ExtractSymbolFromDescription(description);
        if (symbol == null) return;

        if (!TryParseDate(dateStr, out var date)) return;
        if (!TryParseDecimal(amountStr, out var amount)) return;

        dividends.Add(new RawDividend(symbol, currency, date, amount));
    }

    private static void TryParseWithholdingTax(List<string> fields, List<RawWithholdingTax> taxes)
    {
        if (fields.Count < 5) return;

        var discriminator = Clean(fields[1]);
        if (discriminator != "Data") return;

        var currency = Clean(fields[2]);
        var dateStr = Clean(fields[3]);
        var description = Clean(fields[4]);
        var amountStr = Clean(fields[5]);

        if (description.Contains("Total", StringComparison.OrdinalIgnoreCase)) return;

        var symbol = ExtractSymbolFromDescription(description);
        if (symbol == null) return;

        if (!TryParseDate(dateStr, out var date)) return;
        if (!TryParseDecimal(amountStr, out var amount)) return;

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
        var current = new System.Text.StringBuilder();

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
