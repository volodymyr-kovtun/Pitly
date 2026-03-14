using Pitly.Core.Models;

namespace Pitly.Core.Parsing;

public interface IStatementParser
{
    ParsedStatement Parse(string content);
}
