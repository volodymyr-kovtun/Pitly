using Microsoft.Extensions.Logging.Abstractions;
using Pitly.Broker.LGT;
using Pitly.Core.Models;

namespace Pitly.Tests;

public class LgtStatementParserTests
{
    private static readonly LgtStatementParser Parser =
        new(NullLogger<LgtStatementParser>.Instance);

    private const string Header =
        "Client designation;Portfolio number;Portfolio;Account;Account designation;" +
        "Account number;IBAN;Booking text;Currency;Amount;Reference currency;" +
        "Amount in reference currency;Exchange rate;Balance;Beneficiary;" +
        "Message for payee;Transaction date;Booking date;Value date;Order no.;Order type";

    private static string Csv(params string[] dataRows) =>
        Header + "\n" + string.Join("\n", dataRows);

    [Fact]
    public void Parse_BuyWithUnits_ParsesCorrectly()
    {
        var csv = Csv(
            "\"Surname Name\";\"1111222.004\";\"1111222.004 Client portfolio\";\"1111222.035 USD\";" +
            "\"USD account\";\"1111222035\";\"CH64083350\";" +
            "\"Order 293441314   Buy 30.00 units  Ut Inve SP 500 (11358996) \";" +
            "\"USD\";\"-40515.37\";\"USD\";\"-40515.37\";\"1\";\"112080.15\";\"\";\"\";\"17.11.2025\";" +
            "\"17.11.2025\";\"19.11.2025\";\"293441314\";\"Buy\"");

        var parsed = Parser.Parse(csv);

        var trade = Assert.Single(parsed.Trades);
        Assert.Equal(TradeType.Buy, trade.Type);
        Assert.Equal("Ut Inve SP 500", trade.Symbol);
        Assert.Equal("11358996", trade.Isin);
        Assert.Equal("USD", trade.Currency);
        Assert.Equal(30m, trade.Quantity);
        Assert.Equal(40515.37m, trade.Proceeds);
        Assert.Equal(40515.37m / 30m, trade.Price);
        Assert.Equal(0m, trade.Commission);
        Assert.Equal(new DateTime(2025, 11, 17), trade.DateTime);
    }

    [Fact]
    public void Parse_SellWithUnits_ParsesCorrectly()
    {
        var csv = Csv(
            "\"Surname Name\";\"1111222.004\";\"portfolio\";\"account\";" +
            "\"USD account\";\"1111222035\";\"CH64\";" +
            "\"Order 293441291   Sell 1,830.00 units  Ut iShs USA SRI (31608368) \";" +
            "\"USD\";\"31831.16\";\"USD\";\"31831.16\";\"1\";\"152595.52\";\"\";\"\";\"17.11.2025\";" +
            "\"17.11.2025\";\"19.11.2025\";\"293441291\";\"Sell\"");

        var parsed = Parser.Parse(csv);

        var trade = Assert.Single(parsed.Trades);
        Assert.Equal(TradeType.Sell, trade.Type);
        Assert.Equal("Ut iShs USA SRI", trade.Symbol);
        Assert.Equal("31608368", trade.Isin);
        Assert.Equal(1830m, trade.Quantity);
        Assert.Equal(31831.16m, trade.Proceeds);
    }

    [Fact]
    public void Parse_BondBuyWithFaceValue_ParsesCorrectly()
    {
        var csv = Csv(
            "\"Surname Name\";\"1111222.004\";\"portfolio\";\"account\";" +
            "\"USD account\";\"1111222035\";\"CH64\";" +
            "\"Order 293441324   Buy USD 25,000.00  FLR GS 31 (149802833) \";" +
            "\"USD\";\"-24994.17\";\"USD\";\"-24994.17\";\"1\";\"46427.84\";\"\";\"\";\"17.11.2025\";" +
            "\"17.11.2025\";\"18.11.2025\";\"293441324\";\"Buy\"");

        var parsed = Parser.Parse(csv);

        var trade = Assert.Single(parsed.Trades);
        Assert.Equal(TradeType.Buy, trade.Type);
        Assert.Equal("FLR GS 31", trade.Symbol);
        Assert.Equal("149802833", trade.Isin);
        Assert.Equal(25000m, trade.Quantity);
        Assert.Equal(24994.17m, trade.Proceeds);
        Assert.Equal(24994.17m / 25000m, trade.Price);
    }

    [Fact]
    public void Parse_BondRedemptionAtMaturity_ParsesCorrectly()
    {
        var csv = Csv(
            "\"Surname Name\";\"1111222.004\";\"portfolio\";\"account\";" +
            "\"USD account\";\"1111222035\";\"CH64\";" +
            "\"Order 213638906 Redemption at Maturity 2 Microsoft 23\";" +
            "\"USD\";\"28000\";\"USD\";\"28000\";\"1\";\"31448.38\";\"\";\"\";\"08.08.2023\";" +
            "\"07.08.2023\";\"08.08.2023\";\"213638906\";\"Redemption\"");

        var parsed = Parser.Parse(csv);

        var trade = Assert.Single(parsed.Trades);
        Assert.Equal(TradeType.Sell, trade.Type);
        Assert.Equal("2 Microsoft 23", trade.Symbol);
        Assert.Equal(28000m, trade.Quantity);
        Assert.Equal(28000m, trade.Proceeds);
        Assert.Equal(1.0m, trade.Price);
    }

    [Fact]
    public void Parse_RedemptionPriorToMaturity_ParsesAsSell()
    {
        var csv = Csv(
            "\"Surname Name\";\"1111222.004\";\"portfolio\";\"account\";" +
            "\"USD account\";\"1111222035\";\"CH64\";" +
            "\"Order 288555607 Redemption prior to Maturity 5.507 Amgen 26\";" +
            "\"USD\";\"20000\";\"USD\";\"20000\";\"1\";\"59338.66\";\"\";\"\";\"30.09.2025\";" +
            "\"30.09.2025\";\"30.09.2025\";\"288555607\";\"Redemption prior to maturity\"");

        var parsed = Parser.Parse(csv);

        var trade = Assert.Single(parsed.Trades);
        Assert.Equal(TradeType.Sell, trade.Type);
        Assert.Equal("5.507 Amgen 26", trade.Symbol);
        Assert.Equal(20000m, trade.Quantity);
    }

    [Fact]
    public void Parse_Subscription_ParsesAsBuy()
    {
        var csv = Csv(
            "\"Surname Name\";\"1111222.004\";\"portfolio\";\"account\";" +
            "\"USD account\";\"1111222035\";\"CH64\";" +
            "\"Order 293548736   Subscription 260.00 units  Ut Pict-ST MM USD I (1226094) \";" +
            "\"USD\";\"-44771.64\";\"USD\";\"-44771.64\";\"1\";\"1656.2\";\"\";\"\";\"18.11.2025\";" +
            "\"18.11.2025\";\"19.11.2025\";\"293548736\";\"Subscription\"");

        var parsed = Parser.Parse(csv);

        var trade = Assert.Single(parsed.Trades);
        Assert.Equal(TradeType.Buy, trade.Type);
        Assert.Equal("Ut Pict-ST MM USD I", trade.Symbol);
        Assert.Equal("1226094", trade.Isin);
        Assert.Equal(260m, trade.Quantity);
        Assert.Equal(44771.64m, trade.Proceeds);
    }

    [Fact]
    public void Parse_DividendCash_ParsesCorrectly()
    {
        var csv = Csv(
            "\"Surname Name\";\"1111222.004\";\"portfolio\";\"account\";" +
            "\"USD account\";\"1111222035\";\"CH64\";" +
            "\"Order 297537145 Dividend Cash Ut VanEck Sem\";" +
            "\"USD\";\"107.98\";\"USD\";\"107.98\";\"1\";\"953.78\";\"\";\"\";\"26.12.2025\";" +
            "\"26.12.2025\";\"26.12.2025\";\"297537145\";\"Dividend Cash\"");

        var parsed = Parser.Parse(csv);

        Assert.Empty(parsed.Trades);
        var dividend = Assert.Single(parsed.Dividends);
        Assert.Equal("Ut VanEck Sem", dividend.Symbol);
        Assert.Equal("USD", dividend.Currency);
        Assert.Equal(107.98m, dividend.Amount);
        Assert.Equal(new DateTime(2025, 12, 26), dividend.Date);
    }

    [Fact]
    public void Parse_DividendInheritsValorFromBuyRow()
    {
        // Dividend rows don't have a (valor) in parens, but if a Buy row for the same
        // security appears elsewhere in the statement, the valor should carry over.
        var csv = Csv(
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 100 Buy 10.00 units  Ut VanEck Sem (32071836) \";" +
            "\"USD\";\"-1500\";\"USD\";\"-1500\";\"1\";\"-500\";\"\";\"\";\"01.01.2025\";" +
            "\"01.01.2025\";\"01.01.2025\";\"100\";\"Buy\"",
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 200 Dividend Cash Ut VanEck Sem\";" +
            "\"USD\";\"107.98\";\"USD\";\"107.98\";\"1\";\"500\";\"\";\"\";\"26.12.2025\";" +
            "\"26.12.2025\";\"26.12.2025\";\"200\";\"Dividend Cash\"");

        var parsed = Parser.Parse(csv);

        var dividend = Assert.Single(parsed.Dividends);
        Assert.Equal("Ut VanEck Sem", dividend.Symbol);
        Assert.Equal("32071836", dividend.Isin);
    }

    [Fact]
    public void Parse_IgnoresForexFeeAndMoneyMarketRows()
    {
        var csv = Csv(
            // Forex Spot — should be ignored
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 296077278  Forex Swap Near Leg  EUR 68,663.63  exchange rate EUR/USD 1.1651\";" +
            "\"USD\";\"80000\";\"USD\";\"80000\";\"1\";\"2131.2\";\"\";\"\";\"10.12.2025\";" +
            "\"10.12.2025\";\"12.12.2025\";\"296077278\";\"Forex Swap Near Leg\"",
            // EAM Fees — should be ignored
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order no.: 289007112 payment  TRIGON FAMILY OFFICE AG\";" +
            "\"USD\";\"-1946.57\";\"USD\";\"-1946.57\";\"1\";\"77392.09\";\"TRIGON FAMILY OFFICE AG\";" +
            "\"MANAGEMENT FEE Q3-2025\";\"03.10.2025\";\"03.10.2025\";\"03.10.2025\";\"289007112\";\"EAM Fees\"",
            // Pricing: Fee charge — should be ignored
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Quarterly fees\";" +
            "\"USD\";\"-1285.4\";\"USD\";\"-1285.4\";\"1\";\"845.8\";\"\";\"\";\"31.12.2025\";" +
            "\"18.12.2025\";\"31.12.2025\";\"296829661\";\"Pricing: Fee charge\"",
            // Money market Close — should be ignored
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 240170544  Close: Fiduciary call money USD, 20.11.2023, 48h (CHFID.277940)\";" +
            "\"USD\";\"103480.67\";\"USD\";\"103480.67\";\"1\";\"152967.75\";\"\";\"\";\"28.06.2024\";" +
            "\"28.06.2024\";\"02.07.2024\";\"240170544\";\"Close\"",
            // Closing entry — should be ignored
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Closing entry 270584432\";" +
            "\"USD\";\"-6.71\";\"USD\";\"-6.71\";\"1\";\"11251.18\";\"\";\"\";\"31.03.2025\";" +
            "\"31.03.2025\";\"31.03.2025\";\"270584432\";\"Closing entry\"",
            // A real trade to pass the "no trades or dividends" guard
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 100 Buy 10.00 units  Ut Test (999) \";" +
            "\"USD\";\"-100\";\"USD\";\"-100\";\"1\";\"0\";\"\";\"\";\"01.01.2025\";" +
            "\"01.01.2025\";\"01.01.2025\";\"100\";\"Buy\"");

        var parsed = Parser.Parse(csv);

        Assert.Single(parsed.Trades);
        Assert.Empty(parsed.Dividends);
    }

    [Fact]
    public void Parse_RejectsInterestRows()
    {
        var csv = Csv(
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 293602630 Interest 4.75 Pfizer Invt 33\";" +
            "\"USD\";\"475\";\"USD\";\"475\";\"1\";\"2131.2\";\"\";\"\";\"19.11.2025\";" +
            "\"18.11.2025\";\"19.11.2025\";\"293602630\";\"Interest\"");

        var ex = Assert.Throws<FormatException>(() => Parser.Parse(csv));
        Assert.Contains("Bond coupon interest", ex.Message);
    }

    [Fact]
    public void Parse_RejectsFinalLiquidationPayment()
    {
        var csv = Csv(
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 286818632 Final Liquidation Payment Ut iShs Edg Eur\";" +
            "\"USD\";\"29099.98\";\"USD\";\"29099.98\";\"1\";\"60480.93\";\"\";\"\";\"10.09.2025\";" +
            "\"11.09.2025\";\"10.09.2025\";\"286818632\";\"Final liquidation payment\"");

        var ex = Assert.Throws<FormatException>(() => Parser.Parse(csv));
        Assert.Contains("Final liquidation", ex.Message);
    }

    [Fact]
    public void Parse_RejectsUnrecognisedOrderType()
    {
        var csv = Csv(
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 1 Something weird\";" +
            "\"USD\";\"100\";\"USD\";\"100\";\"1\";\"0\";\"\";\"\";\"01.01.2025\";" +
            "\"01.01.2025\";\"01.01.2025\";\"1\";\"Something weird\"");

        var ex = Assert.Throws<FormatException>(() => Parser.Parse(csv));
        Assert.Contains("Unrecognised LGT order type", ex.Message);
    }

    [Fact]
    public void Parse_DeterminesStatementYearFromLatestTransaction()
    {
        var csv = Csv(
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 100 Buy 10.00 units  Ut Test (999) \";" +
            "\"USD\";\"-100\";\"USD\";\"-100\";\"1\";\"0\";\"\";\"\";\"15.03.2024\";" +
            "\"15.03.2024\";\"15.03.2024\";\"100\";\"Buy\"",
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 200 Sell 10.00 units  Ut Test (999) \";" +
            "\"USD\";\"110\";\"USD\";\"110\";\"1\";\"0\";\"\";\"\";\"20.01.2025\";" +
            "\"20.01.2025\";\"20.01.2025\";\"200\";\"Sell\"");

        var parsed = Parser.Parse(csv);

        Assert.Equal(2025, parsed.StatementYear);
    }

    [Fact]
    public void Parse_BondBuyAndRedemptionShareSymbolForFifo()
    {
        // Verify that a bond buy and its redemption produce the same Symbol
        // so that FIFO can match them (even though redemption lacks a valor).
        var csv = Csv(
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 100   Buy USD 20,000.00  5.507 Amgen 26 (125198651) \";" +
            "\"USD\";\"-20501.89\";\"USD\";\"-20501.89\";\"1\";\"0\";\"\";\"\";\"13.02.2024\";" +
            "\"13.02.2024\";\"15.02.2024\";\"100\";\"Buy\"",
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 200 Redemption prior to Maturity 5.507 Amgen 26\";" +
            "\"USD\";\"20000\";\"USD\";\"20000\";\"1\";\"0\";\"\";\"\";\"30.09.2025\";" +
            "\"30.09.2025\";\"30.09.2025\";\"200\";\"Redemption prior to maturity\"");

        var parsed = Parser.Parse(csv);

        Assert.Equal(2, parsed.Trades.Count);
        var buy = parsed.Trades[0];
        var sell = parsed.Trades[1];

        Assert.Equal(TradeType.Buy, buy.Type);
        Assert.Equal(TradeType.Sell, sell.Type);
        Assert.Equal(buy.Symbol, sell.Symbol);
        // Redemption should inherit the valor from the buy
        Assert.Equal("125198651", buy.Isin);
        Assert.Equal("125198651", sell.Isin);
    }

    [Fact]
    public void Parse_EmptyFile_Throws()
    {
        var ex = Assert.Throws<FormatException>(() => Parser.Parse(""));
        Assert.Contains("empty", ex.Message);
    }

    [Fact]
    public void Parse_MissingRequiredColumn_Throws()
    {
        var csv = "Client designation;Portfolio number;Booking text;Currency;Amount\n" +
                  "\"N\";\"P\";\"text\";\"USD\";\"100\"";

        var ex = Assert.Throws<FormatException>(() => Parser.Parse(csv));
        Assert.Contains("Missing required column", ex.Message);
    }

    [Fact]
    public void Parse_MixedTradesAndDividends_ParsesAll()
    {
        var csv = Csv(
            // Buy
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 100   Buy 30.00 units  Ut Inve SP 500 (11358996) \";" +
            "\"USD\";\"-40515.37\";\"USD\";\"-40515.37\";\"1\";\"0\";\"\";\"\";\"17.11.2025\";" +
            "\"17.11.2025\";\"19.11.2025\";\"100\";\"Buy\"",
            // Sell
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 200   Sell 1,830.00 units  Ut iShs USA SRI (31608368) \";" +
            "\"USD\";\"31831.16\";\"USD\";\"31831.16\";\"1\";\"0\";\"\";\"\";\"17.11.2025\";" +
            "\"17.11.2025\";\"19.11.2025\";\"200\";\"Sell\"",
            // Dividend
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 300 Dividend Cash Ut VanEck Sem\";" +
            "\"USD\";\"107.98\";\"USD\";\"107.98\";\"1\";\"0\";\"\";\"\";\"26.12.2025\";" +
            "\"26.12.2025\";\"26.12.2025\";\"300\";\"Dividend Cash\"",
            // Forex Spot — ignored
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 400  Forex Spot  CHF 37,500.00  exchange rate USD/CHF 0.898501\";" +
            "\"USD\";\"-41736.18\";\"USD\";\"-41736.18\";\"1\";\"0\";\"\";\"\";\"28.06.2024\";" +
            "\"28.06.2024\";\"02.07.2024\";\"400\";\"Forex Spot\"");

        var parsed = Parser.Parse(csv);

        Assert.Equal(2, parsed.Trades.Count);
        Assert.Single(parsed.Dividends);
        Assert.Empty(parsed.WithholdingTaxes);
        Assert.Equal(2025, parsed.StatementYear);
    }

    [Fact]
    public void Parse_HandlesBomPrefix()
    {
        var csv = "\uFEFF" + Csv(
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 100 Buy 5.00 units  Ut Test (999) \";" +
            "\"USD\";\"-500\";\"USD\";\"-500\";\"1\";\"0\";\"\";\"\";\"01.06.2025\";" +
            "\"01.06.2025\";\"01.06.2025\";\"100\";\"Buy\"");

        var parsed = Parser.Parse(csv);
        Assert.Single(parsed.Trades);
    }

    [Fact]
    public void Parse_EamFeesStornoIgnored()
    {
        var csv = Csv(
            // EAM Gebühren Storno
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"EAM Gebühren Storno: USD\";" +
            "\"USD\";\"1705.64\";\"USD\";\"1705.64\";\"1\";\"-175.5\";\"1875 FINANCE SA\";" +
            "\"MGT. FEE Q2.2024\";\"25.06.2024\";\"28.06.2024\";\"25.06.2024\";\"239854120\";" +
            "\"EAM Gebühren Storno\"",
            // Real trade to avoid empty-result error
            "\"N\";\"P\";\"P\";\"A\";\"A\";\"N\";\"I\";" +
            "\"Order 100 Buy 1.00 units  Ut Test (1) \";" +
            "\"USD\";\"-10\";\"USD\";\"-10\";\"1\";\"0\";\"\";\"\";\"01.01.2025\";" +
            "\"01.01.2025\";\"01.01.2025\";\"100\";\"Buy\"");

        var parsed = Parser.Parse(csv);
        Assert.Single(parsed.Trades);
    }
}
