using Pitly.Broker.InteractiveBrokers;
using Pitly.Broker.LGT;
using Pitly.Broker.Trading212;
using Pitly.Core.Models;
using Pitly.Core.Parsing;
using static Pitly.Core.Parsing.CsvHelpers;

namespace Pitly.Api.Services;

public class StatementParserDispatcher : IStatementParser
{
    private readonly InteractiveBrokersStatementParser _ibParser;
    private readonly Trading212StatementParser _t212Parser;
    private readonly LgtStatementParser _lgtParser;

    public StatementParserDispatcher(
        InteractiveBrokersStatementParser ibParser,
        Trading212StatementParser t212Parser,
        LgtStatementParser lgtParser)
    {
        _ibParser = ibParser;
        _t212Parser = t212Parser;
        _lgtParser = lgtParser;
    }

    public ParsedStatement Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new FormatException("File is empty.");

        var newlineIndex = content.IndexOfAny('\n', '\r');
        var firstLine = newlineIndex > 0
            ? content[..newlineIndex]
            : content;

        var firstField = ParseCsvLine(firstLine)
            .FirstOrDefault();

        if (string.Equals(Clean(firstField), "Action", StringComparison.OrdinalIgnoreCase))
            return _t212Parser.Parse(content);

        // LGT exports use semicolons; the canonical first column is "Client designation".
        var firstSemicolonField = Clean(firstLine.Split(';', 2).FirstOrDefault());
        if (string.Equals(firstSemicolonField, "Client designation", StringComparison.OrdinalIgnoreCase))
            return _lgtParser.Parse(content);

        if (firstLine.Contains("Statement,", StringComparison.Ordinal) ||
            firstLine.Contains("Trades,", StringComparison.Ordinal) ||
            firstLine.Contains("Dividends,", StringComparison.Ordinal) ||
            firstLine.Contains("Withholding Tax,", StringComparison.Ordinal))
            return _ibParser.Parse(content);

        throw new FormatException(
            "Unrecognized file format. Please upload an Interactive Brokers, Trading 212, or LGT CSV export.");
    }
}
