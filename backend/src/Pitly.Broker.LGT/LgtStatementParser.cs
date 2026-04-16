using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Pitly.Core.Models;
using Pitly.Core.Parsing;
using static Pitly.Core.Parsing.CsvHelpers;

namespace Pitly.Broker.LGT;

/// <summary>
/// Parses "Account bookings" CSV exports from LGT Bank.
///
/// LGT exports use semicolon-separated values with the following relevant columns:
/// Currency, Amount, Booking text, Transaction date, Order type.
///
/// The parser recognises the following <c>Order type</c> values:
///  - "Buy", "Sell"                                 -> capital gains trades
///  - "Subscription"                                -> fund unit purchase (treated as Buy)
///  - "Redemption", "Redemption at maturity",
///    "Redemption prior to maturity"                -> bond/fund redemption (treated as Sell)
///  - "Dividend Cash"                               -> dividend (no withholding info available)
///
/// The following order types are silently ignored — they do not affect PIT-38 capital
/// gains or dividend lines under the model Pitly currently exposes:
///  - Forex Spot / Forex Swap Near Leg / Forex Forward Maturity (currency conversions)
///  - Place / Close / Decrease / Opening (money-market / fiduciary deposit movements)
///  - Pricing: Fee charge, EAM Fees, EAM Gebühren Storno (administrative fees)
///  - Closing entry (sub-1 USD account-closing adjustments)
///
/// Bond coupon income ("Interest", "Interest payment", "Interest Payment") and
/// "Final liquidation payment" rows are reported back to the user via <see cref="FormatException"/>
/// when present, so that they can be handled manually — Pitly does not currently model
/// interest income or liquidations without an explicit unit count.
/// </summary>
public partial class LgtStatementParser : IStatementParser
{
    private const char Separator = ';';

    private static readonly string[] DateFormats =
        ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"];

    private readonly ILogger<LgtStatementParser> _logger;

    public LgtStatementParser(ILogger<LgtStatementParser> logger)
    {
        _logger = logger;
    }

    public ParsedStatement Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new FormatException("File is empty.");

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            throw new FormatException("File contains no data rows.");

        var headerFields = ParseSemicolonLine(lines[0].TrimEnd('\r'));
        var columns = BuildColumnMap(headerFields);

        RequireColumn(columns, "order type");
        RequireColumn(columns, "booking text");
        RequireColumn(columns, "amount");
        RequireColumn(columns, "currency");
        RequireColumn(columns, "transaction date");

        var rawRows = new List<RawRow>();
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseSemicolonLine(line);
            var orderType = GetField(fields, columns, "order type") ?? string.Empty;
            var bookingText = GetField(fields, columns, "booking text") ?? string.Empty;
            var currency = GetField(fields, columns, "currency") ?? string.Empty;
            var amountStr = GetField(fields, columns, "amount") ?? string.Empty;
            var dateStr = GetField(fields, columns, "transaction date") ?? string.Empty;

            rawRows.Add(new RawRow(
                LineNumber: i + 1,
                OrderType: orderType,
                BookingText: bookingText,
                Currency: currency,
                AmountText: amountStr,
                DateText: dateStr));
        }

        // First pass — build a "name -> valor" mapping from rows whose booking text exposes
        // both. Buy/Sell/Subscription rows quote the LGT valor in trailing parentheses, but
        // bond redemptions and dividend rows often omit it. Mapping ensures that the same
        // security gets a consistent FIFO key throughout the year.
        var nameToValor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rawRows)
        {
            var instrument = ExtractInstrument(row.BookingText, row.OrderType);
            if (instrument is { Name: var n, Valor: { Length: > 0 } v })
                nameToValor.TryAdd(NormalizeName(n), v);
        }

        var trades = new List<Trade>();
        var dividends = new List<RawDividend>();
        var withholdingTaxes = new List<RawWithholdingTax>();
        var skipped = 0;

        foreach (var row in rawRows)
        {
            var category = ClassifyOrderType(row.OrderType);
            switch (category)
            {
                case OrderCategory.Trade:
                    trades.Add(ParseTrade(row, nameToValor));
                    break;

                case OrderCategory.Dividend:
                    dividends.Add(ParseDividend(row, nameToValor));
                    break;

                case OrderCategory.UnsupportedIncome:
                    throw new FormatException(
                        $"Unsupported LGT order type '{row.OrderType.Trim()}' on line {row.LineNumber}. " +
                        "Bond coupon interest and similar income types are not yet handled by Pitly's PIT-38 calculation. " +
                        "Remove these rows from the export or split the file before re-uploading.");

                case OrderCategory.UnsupportedTrade:
                    throw new FormatException(
                        $"Unsupported LGT order type '{row.OrderType.Trim()}' on line {row.LineNumber}. " +
                        "Final liquidation payments do not include a unit count and need to be entered manually for safe FIFO matching.");

                case OrderCategory.IgnoredCashflow:
                    skipped++;
                    break;

                case OrderCategory.Unknown:
                    throw new FormatException(
                        $"Unrecognised LGT order type '{row.OrderType.Trim()}' on line {row.LineNumber}. " +
                        "Pitly cannot decide whether this affects the PIT-38 calculation safely.");
            }
        }

        if (trades.Count == 0 && dividends.Count == 0)
        {
            throw new FormatException(
                "No trades or dividends found. Please upload a valid LGT account bookings CSV export.");
        }

        _logger.LogInformation(
            "Parsed LGT statement: {Trades} trades, {Dividends} dividends, {Skipped} ignored cashflow rows",
            trades.Count, dividends.Count, skipped);

        return new ParsedStatement(
            trades,
            dividends,
            withholdingTaxes,
            StatementYear: DetermineStatementYear(trades, dividends));
    }

    private record RawRow(
        int LineNumber,
        string OrderType,
        string BookingText,
        string Currency,
        string AmountText,
        string DateText);

    private record Instrument(string Name, string? Valor);

    private enum OrderCategory
    {
        Trade,
        Dividend,
        IgnoredCashflow,
        UnsupportedIncome,
        UnsupportedTrade,
        Unknown
    }

    private Trade ParseTrade(RawRow row, IReadOnlyDictionary<string, string> nameToValor)
    {
        var amount = RequireDecimal(row.AmountText, row.LineNumber, "amount");
        var date = RequireDate(row.DateText, row.LineNumber, "transaction date");
        var currency = RequireText(row.Currency, row.LineNumber, "currency").ToUpperInvariant();

        var tradeType = IsBuyLikeOrderType(row.OrderType) ? TradeType.Buy : TradeType.Sell;

        var instrument = ExtractInstrument(row.BookingText, row.OrderType)
            ?? throw new FormatException(
                $"Could not extract security name from booking text '{row.BookingText.Trim()}' on line {row.LineNumber}.");

        var quantity = ExtractQuantity(row.BookingText, tradeType, row.OrderType, amount, row.LineNumber);
        if (quantity <= 0)
        {
            throw new FormatException(
                $"Trade row on line {row.LineNumber} has non-positive quantity '{quantity}'.");
        }

        var proceeds = Math.Abs(amount);
        var price = proceeds / quantity;

        var valor = !string.IsNullOrEmpty(instrument.Valor)
            ? instrument.Valor
            : nameToValor.GetValueOrDefault(NormalizeName(instrument.Name));

        return new Trade(
            Symbol: instrument.Name,
            Currency: currency,
            DateTime: date,
            Quantity: quantity,
            Price: price,
            Proceeds: proceeds,
            Commission: 0m,
            CommissionCurrency: currency,
            RealizedPnL: 0m,
            Type: tradeType,
            Isin: valor);
    }

    private RawDividend ParseDividend(RawRow row, IReadOnlyDictionary<string, string> nameToValor)
    {
        var amount = RequireDecimal(row.AmountText, row.LineNumber, "amount");
        var date = RequireDate(row.DateText, row.LineNumber, "transaction date");
        var currency = RequireText(row.Currency, row.LineNumber, "currency").ToUpperInvariant();

        if (amount <= 0)
        {
            throw new FormatException(
                $"Dividend row on line {row.LineNumber} has non-positive amount '{amount}'. " +
                "LGT dividend reversals are not yet supported.");
        }

        var instrument = ExtractInstrument(row.BookingText, row.OrderType)
            ?? throw new FormatException(
                $"Could not extract security name from booking text '{row.BookingText.Trim()}' on line {row.LineNumber}.");

        var valor = !string.IsNullOrEmpty(instrument.Valor)
            ? instrument.Valor
            : nameToValor.GetValueOrDefault(NormalizeName(instrument.Name));

        return new RawDividend(
            Symbol: instrument.Name,
            Currency: currency,
            Date: date,
            Amount: amount,
            Isin: valor);
    }

    private static OrderCategory ClassifyOrderType(string orderType)
    {
        var normalized = NormalizeOrderType(orderType);
        if (normalized.Length == 0)
            return OrderCategory.IgnoredCashflow;

        if (normalized is "buy" or "sell" or "subscription"
            || normalized.StartsWith("redemption", StringComparison.Ordinal))
        {
            return OrderCategory.Trade;
        }

        if (normalized is "dividend cash" or "dividend")
            return OrderCategory.Dividend;

        if (normalized is "interest" or "interest payment")
            return OrderCategory.UnsupportedIncome;

        if (normalized is "final liquidation payment")
            return OrderCategory.UnsupportedTrade;

        if (normalized.StartsWith("forex", StringComparison.Ordinal)
            || normalized is "place" or "close" or "decrease" or "opening" or "increase"
            || normalized is "pricing: fee charge" or "eam fees" or "eam gebühren storno"
            || normalized is "closing entry"
            || normalized is "transfer" or "deposit" or "withdrawal")
        {
            return OrderCategory.IgnoredCashflow;
        }

        return OrderCategory.Unknown;
    }

    private static bool IsBuyLikeOrderType(string orderType)
    {
        var normalized = NormalizeOrderType(orderType);
        return normalized is "buy" or "subscription";
    }

    private static Instrument? ExtractInstrument(string bookingText, string orderType)
    {
        var text = (bookingText ?? string.Empty).Trim();
        if (text.Length == 0) return null;

        // Strip leading "Order [no.:] NNN " prefix.
        var match = OrderPrefixRegex().Match(text);
        if (match.Success)
            text = text[match.Length..];

        text = text.Trim();
        if (text.Length == 0) return null;

        // Strip the action verb that follows the order number; keep just the security
        // description. Action verbs and qualifiers we know about, longest-match-first.
        var actionStripped = StripActionPrefix(text, orderType);
        if (actionStripped is null)
            return null;

        // Trailing "(VALOR)" — LGT's Swiss valoren-nummer.
        string? valor = null;
        var valorMatch = TrailingValorRegex().Match(actionStripped);
        if (valorMatch.Success)
        {
            valor = valorMatch.Groups["valor"].Value;
            actionStripped = actionStripped[..valorMatch.Index].TrimEnd();
        }

        // Some money-market / fiduciary descriptions trail with ", DD.MM.YYYY, 48h (CHFID.xxxxx)"
        // — strip anything past a comma so the security key stays stable.
        var commaIndex = actionStripped.IndexOf(',');
        if (commaIndex > 0)
            actionStripped = actionStripped[..commaIndex].TrimEnd();

        if (string.IsNullOrWhiteSpace(actionStripped))
            return null;

        return new Instrument(actionStripped, valor);
    }

    private static string? StripActionPrefix(string text, string orderType)
    {
        // The booking text usually begins with the action keyword(s) followed by a quantity
        // expression: e.g. "Buy 30.00 units NAME", "Buy USD 25,000.00 NAME",
        // "Sell 1,830.00 units NAME", "Subscription 260.00 units NAME",
        // "Redemption at Maturity NAME", "Redemption prior to Maturity NAME",
        // "Dividend Cash NAME", "Final Liquidation Payment NAME",
        // "Interest 5.507 Amgen 26", "Interest payment: ...", "Interest Payment: ...".
        var actionMatch = ActionPrefixRegex().Match(text);
        if (actionMatch.Success)
            return text[actionMatch.Length..].Trim();

        // Fallback: derive from the explicit Order type column when the booking text is
        // unusual. Order type is authoritative for classification, so we take everything
        // after the matched order-type prefix.
        var prefix = orderType?.Trim() ?? string.Empty;
        if (prefix.Length > 0 && text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return text[prefix.Length..].Trim();

        return text;
    }

    private static decimal ExtractQuantity(
        string bookingText,
        TradeType tradeType,
        string orderType,
        decimal amount,
        int lineNumber)
    {
        var text = bookingText ?? string.Empty;

        // "Buy 30.00 units NAME" / "Sell 1,830.00 units NAME" / "Subscription 260.00 units NAME"
        var unitsMatch = UnitsQuantityRegex().Match(text);
        if (unitsMatch.Success
            && TryParseInvariantDecimal(unitsMatch.Groups["qty"].Value, out var unitsQty))
        {
            return unitsQty;
        }

        // "Buy USD 25,000.00 NAME" / "Sell USD 20,000.00 NAME" — bond face-value trades.
        // Treat face value as quantity, price = proceeds / face-value (so the bond price ratio).
        var faceValueMatch = FaceValueQuantityRegex().Match(text);
        if (faceValueMatch.Success
            && TryParseInvariantDecimal(faceValueMatch.Groups["qty"].Value, out var faceQty))
        {
            return faceQty;
        }

        // Bond redemptions come without an explicit quantity in the booking text. The cash
        // amount equals the face value redeemed at par, so treat |amount| as the quantity
        // (and the calculator will derive price = 1.0). This mirrors how the Buy side was
        // booked.
        if (tradeType == TradeType.Sell
            && NormalizeOrderType(orderType).StartsWith("redemption", StringComparison.Ordinal))
        {
            return Math.Abs(amount);
        }

        throw new FormatException(
            $"Could not determine quantity from booking text '{text.Trim()}' on line {lineNumber}.");
    }

    private static string NormalizeName(string name) =>
        string.Join(" ", name
            .Trim()
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string NormalizeOrderType(string orderType) =>
        string.Join(" ", (orderType ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static int DetermineStatementYear(
        IEnumerable<Trade> trades,
        IEnumerable<RawDividend> dividends)
    {
        var years = trades.Select(t => t.DateTime.Year)
            .Concat(dividends.Select(d => d.Date.Year))
            .ToList();

        if (years.Count == 0)
        {
            throw new FormatException(
                "No trades or dividends found. Please upload a valid LGT account bookings CSV export.");
        }

        return years.Max();
    }

    private static List<string> ParseSemicolonLine(string line)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == Separator && !inQuotes)
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

    private static Dictionary<string, int> BuildColumnMap(List<string> headerFields)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headerFields.Count; i++)
        {
            var name = NormalizeHeader(headerFields[i]);
            if (name.Length == 0)
                continue;
            map[name] = i;
        }
        return map;
    }

    private static string NormalizeHeader(string header) =>
        string.Join(" ", Clean(header)
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static void RequireColumn(IReadOnlyDictionary<string, int> columns, string columnName)
    {
        if (!columns.ContainsKey(columnName))
        {
            throw new FormatException(
                $"File does not appear to be an LGT account bookings export. Missing required column '{columnName}'.");
        }
    }

    private static string? GetField(
        List<string> fields,
        IReadOnlyDictionary<string, int> columns,
        string columnName)
    {
        if (!columns.TryGetValue(columnName, out var index) || index >= fields.Count)
            return null;

        var value = Clean(fields[index]);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static decimal RequireDecimal(string raw, int lineNumber, string fieldName)
    {
        if (!TryParseInvariantDecimal(raw, out var value))
        {
            throw new FormatException(
                $"Could not parse {fieldName} '{raw}' on line {lineNumber}.");
        }
        return value;
    }

    private static DateTime RequireDate(string raw, int lineNumber, string fieldName)
    {
        if (!TryParseDate(raw, out var value))
        {
            throw new FormatException(
                $"Could not parse {fieldName} '{raw}' on line {lineNumber}.");
        }
        return value;
    }

    private static string RequireText(string raw, int lineNumber, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new FormatException(
                $"Missing required field '{fieldName}' on line {lineNumber}.");
        }
        return raw.Trim();
    }

    private static bool TryParseInvariantDecimal(string? s, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(s)) return false;
        // LGT exports use '.' decimal separator and may include thousands separators (',').
        return decimal.TryParse(
            s.Replace(",", string.Empty),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static bool TryParseDate(string s, out DateTime result) =>
        DateTime.TryParseExact(
            (s ?? string.Empty).Trim(),
            DateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);

    [GeneratedRegex(@"^\s*Order(?:\s+no\.?:?)?\s+\d+\s*", RegexOptions.IgnoreCase)]
    private static partial Regex OrderPrefixRegex();

    // Strips the action verb plus its quantity expression (when any). Order matters —
    // longer alternatives must come first so e.g. "Dividend Cash" wins over "Dividend",
    // and "Redemption at Maturity" wins over "Redemption". For Buy/Sell/Subscription the
    // quantity is mandatory (either "N units" or "USD/EUR/CHF... N" face value); for
    // Redemption, Dividend and Final Liquidation no quantity follows the verb because the
    // remainder of the string is the security name (which itself often starts with a
    // coupon rate like "2 Microsoft 23" or "5.507 Amgen 26").
    [GeneratedRegex(
        @"^\s*(?:" +
            @"(?:Buy|Sell)\s+(?:[\d,]+(?:\.\d+)?\s+units\s+|(?:USD|EUR|CHF|GBP|PLN)\s+[\d,]+(?:\.\d+)?\s+)" +
            @"|Subscription\s+[\d,]+(?:\.\d+)?\s+units\s+" +
            @"|Redemption\s+(?:prior\s+to|at)\s+Maturity\s+" +
            @"|Redemption\s+" +
            @"|Final\s+Liquidation\s+Payment\s+" +
            @"|Dividend\s+Cash\s+" +
            @"|Dividend\s+" +
        @")",
        RegexOptions.IgnoreCase)]
    private static partial Regex ActionPrefixRegex();

    [GeneratedRegex(@"\((?<valor>\d{4,})\)\s*$")]
    private static partial Regex TrailingValorRegex();

    [GeneratedRegex(@"\b(?<qty>[\d,]+(?:\.\d+)?)\s+units\b", RegexOptions.IgnoreCase)]
    private static partial Regex UnitsQuantityRegex();

    [GeneratedRegex(@"\b(?:USD|EUR|CHF|GBP|PLN)\s+(?<qty>[\d,]+(?:\.\d+)?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FaceValueQuantityRegex();
}
