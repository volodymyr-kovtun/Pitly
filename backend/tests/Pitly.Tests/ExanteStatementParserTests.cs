using Microsoft.Extensions.Logging;
using Moq;
using Pitly.Broker.Exante;
using Pitly.Core.Models;
using Xunit;

namespace Pitly.Tests.Parsers;

public class ExanteStatementParserTests
{
    [Fact]
    public void Parse_ShouldExtractDividendsAndTaxes()
    {
        var csv = """"
"Transaction ID"	"Account ID"	"Symbol ID"	"ISIN"	"Operation Type"	"When"	"Sum"	"Asset"	"EUR equivalent"	"Comment"	"UUID"	"Parent UUID"	"Merchant Name"	"Side"
"855096609"	"NYQ2033.001"	"META.NASDAQ"	"None"	"US TAX"	"2025-12-23 04:03:54"	"-2.29"	"USD"	"-1.94"	"29.0 shares ExD 2025-12-15 PD 2025-12-23 dividend META.NASDAQ 15.23 USD (0.525 per share) tax -2.29 USD (-15.000%) DivCntry US USIncmCode 06"	"bb530fce-3252-4251-a98a-5118933080f7"	"None"	""	""
"855096607"	"NYQ2033.001"	"META.NASDAQ"	"None"	"DIVIDEND"	"2025-12-23 04:03:54"	"15.23"	"USD"	"12.93"	"29.0 shares ExD 2025-12-15 PD 2025-12-23 dividend META.NASDAQ 15.23 USD (0.525 per share) tax -2.29 USD (-15.000%) DivCntry US USIncmCode 06"	"2503b563-b23e-4ed6-b0a2-93c10a067f89"	"None"	""	""
"""";

        var loggerMock = new Mock<ILogger<ExanteStatementParser>>();
        var parser = new ExanteStatementParser(loggerMock.Object);

        var result = parser.Parse(csv);

        Assert.NotNull(result);
        Assert.Equal(2025, result.StatementYear);
        Assert.Empty(result.Trades);
        
        Assert.Single(result.Dividends);
        var div = result.Dividends[0];
        Assert.Equal("META.NASDAQ", div.Symbol);
        Assert.Equal("USD", div.Currency);
        Assert.Equal(15.23m, div.Amount);
        Assert.Equal(new DateTime(2025, 12, 23), div.Date);

        Assert.Single(result.WithholdingTaxes);
        var tax = result.WithholdingTaxes[0];
        Assert.Equal("META.NASDAQ", tax.Symbol);
        Assert.Equal("USD", tax.Currency);
        Assert.Equal(2.29m, tax.Amount);
        Assert.Equal(new DateTime(2025, 12, 23), tax.Date);
    }
}
