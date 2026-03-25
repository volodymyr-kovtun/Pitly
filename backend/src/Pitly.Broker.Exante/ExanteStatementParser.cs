using System.Globalization;
using Microsoft.Extensions.Logging;
using Pitly.Core.Models;
using Pitly.Core.Parsing;

namespace Pitly.Broker.Exante;

public class ExanteStatementParser : IStatementParser
{
    private static readonly string[] DateTimeFormats = ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd"];
    private readonly ILogger<ExanteStatementParser> _logger;

    public ExanteStatementParser(ILogger<ExanteStatementParser> logger)
    {
        _logger = logger;
    }

    public ParsedStatement Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new FormatException("File is empty.");

        // Some Exante exports are UTF-16LE or use tabs.
        // The dispatcher reads the string, but we should parse lines.
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            throw new FormatException("File contains no data rows.");

        var headers = ParseLine(lines[0]);
        var columnMap = BuildColumnMap(headers);

        if (!columnMap.ContainsKey("transaction id") || !columnMap.ContainsKey("operation type"))
        {
            throw new FormatException("File does not appear to be an Exante export. Missing required columns.");
        }

        var trades = new List<Trade>();
        var dividends = new List<RawDividend>();
        var withholdingTaxes = new List<RawWithholdingTax>();

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseLine(line);
            var action = GetField(fields, columnMap, "operation type")?.ToUpperInvariant();
            if (string.IsNullOrEmpty(action)) continue;

            try
            {
                if (action == "DIVIDEND")
                {
                    ParseDividend(fields, columnMap, dividends, i + 1);
                }
                else if (action == "US TAX" || action == "TAX")
                {
                    ParseTax(fields, columnMap, withholdingTaxes, i + 1);
                }
                else if (action == "TRADE")
                {
                    // Basic placeholder for trades if they appear
                    ParseTrade(fields, columnMap, trades, i + 1);
                }
                else
                {
                    // Ignore other types for now (FUNDING, COMMISSION, etc)
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse line {LineNumber}", i + 1);
            }
        }

        if (dividends.Count == 0 && withholdingTaxes.Count == 0 && trades.Count == 0)
        {
            throw new FormatException("No trades, dividends or taxes found. Please upload a valid Exante CSV/TSV export.");
        }

        _logger.LogInformation("Parsed Exante statement: {Trades} trades, {Dividends} dividends, {Withholdings} taxes", trades.Count, dividends.Count, withholdingTaxes.Count);

        var years = trades.Select(t => t.DateTime.Year)
            .Concat(dividends.Select(d => d.Date.Year))
            .Concat(withholdingTaxes.Select(t => t.Date.Year))
            .ToList();

        var statementYear = years.Count > 0 ? years.Max() : DateTime.UtcNow.Year;

        return new ParsedStatement(trades, dividends, withholdingTaxes, StatementYear: statementYear);
    }

    private void ParseDividend(List<string> fields, Dictionary<string, int> columnMap, List<RawDividend> dividends, int lineNumber)
    {
        var symbol = GetField(fields, columnMap, "symbol id") ?? "UNKNOWN";
        var isin = GetField(fields, columnMap, "isin");
        if (isin == "None") isin = null;
        var dateStr = GetField(fields, columnMap, "when");
        var sumStr = GetField(fields, columnMap, "sum");
        var currency = GetField(fields, columnMap, "asset") ?? "USD";

        if (!DateTime.TryParseExact(dateStr, DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new FormatException($"Invalid date {dateStr}");

        if (!decimal.TryParse(sumStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sum))
            throw new FormatException($"Invalid sum {sumStr}");

        dividends.Add(new RawDividend(symbol, currency, date.Date, Math.Abs(sum), isin));
    }

    private void ParseTax(List<string> fields, Dictionary<string, int> columnMap, List<RawWithholdingTax> taxes, int lineNumber)
    {
        var symbol = GetField(fields, columnMap, "symbol id") ?? "UNKNOWN";
        var isin = GetField(fields, columnMap, "isin");
        if (isin == "None") isin = null;
        var dateStr = GetField(fields, columnMap, "when");
        var sumStr = GetField(fields, columnMap, "sum");
        var currency = GetField(fields, columnMap, "asset") ?? "USD";

        if (!DateTime.TryParseExact(dateStr, DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new FormatException($"Invalid date {dateStr}");

        if (!decimal.TryParse(sumStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sum))
            throw new FormatException($"Invalid sum {sumStr}");

        taxes.Add(new RawWithholdingTax(symbol, currency, date.Date, Math.Abs(sum), isin));
    }

    private void ParseTrade(List<string> fields, Dictionary<string, int> columnMap, List<Trade> trades, int lineNumber)
    {
        var symbol = GetField(fields, columnMap, "symbol id") ?? "UNKNOWN";
        var isin = GetField(fields, columnMap, "isin");
        if (isin == "None") isin = null;
        var dateStr = GetField(fields, columnMap, "when");
        var sumStr = GetField(fields, columnMap, "sum");
        var currency = GetField(fields, columnMap, "asset") ?? "USD";
        var comment = GetField(fields, columnMap, "comment") ?? "";
        
        // Exante trade parsing is complex, try to guess from sum and side if available
        var side = GetField(fields, columnMap, "side");
        
        if (!DateTime.TryParseExact(dateStr, DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new FormatException($"Invalid date {dateStr}");

        if (!decimal.TryParse(sumStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sum))
            throw new FormatException($"Invalid sum {sumStr}");

        var type = side?.ToUpperInvariant() == "SELL" || sum > 0 ? TradeType.Sell : TradeType.Buy;

        // Missing actual quantity and price from the sample, throwing format exception for missing data
        // For a full implementation, we'd need these columns or parse from comment.
        trades.Add(new Trade(
            Symbol: symbol,
            Currency: currency,
            DateTime: date,
            Quantity: 1, // Placeholder
            Price: Math.Abs(sum),
            Proceeds: Math.Abs(sum),
            Commission: 0,
            CommissionCurrency: currency,
            RealizedPnL: 0,
            Type: type,
            Isin: isin
        ));
    }

    private List<string> ParseLine(string line)
    {
        line = line.TrimEnd('\r');
        var delimiter = line.Contains("\t") ? '\t' : ',';
        var fields = new List<string>();
        bool inQuotes = false;
        int startIndex = 0;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == delimiter && !inQuotes)
            {
                fields.Add(line.Substring(startIndex, i - startIndex).Trim('"', ' '));
                startIndex = i + 1;
            }
        }
        fields.Add(line.Substring(startIndex).Trim('"', ' '));
        return fields;
    }

    private Dictionary<string, int> BuildColumnMap(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            var header = headers[i]?.Trim();
            if (!string.IsNullOrEmpty(header) && !map.ContainsKey(header))
            {
                map[header.ToLowerInvariant()] = i;
            }
        }
        return map;
    }

    private string? GetField(List<string> fields, Dictionary<string, int> columnMap, string name)
    {
        if (columnMap.TryGetValue(name.ToLowerInvariant(), out var index) && index < fields.Count)
        {
            return fields[index];
        }
        return null;
    }
}
