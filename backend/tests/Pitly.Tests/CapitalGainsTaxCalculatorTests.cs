using Pitly.Core.Models;
using Pitly.Core.Tax;

namespace Pitly.Tests;

public class CapitalGainsTaxCalculatorTests
{
    [Fact]
    public async Task CalculateAsync_UsesIsinForFifoMatching()
    {
        var rateService = new TestRateService(new Dictionary<string, decimal>
        {
            ["USD"] = 5m
        });
        var calculator = new CapitalGainsTaxCalculator(rateService);

        var trades = new List<Trade>
        {
            new(
                Symbol: "ABC",
                Currency: "USD",
                DateTime: new DateTime(2025, 1, 2, 10, 0, 0),
                Quantity: 10m,
                Price: 10m,
                Proceeds: 100m,
                Commission: 0m,
                CommissionCurrency: "USD",
                RealizedPnL: 0m,
                Type: TradeType.Buy,
                Isin: "US1111111111"),
            new(
                Symbol: "ABC",
                Currency: "USD",
                DateTime: new DateTime(2025, 1, 3, 10, 0, 0),
                Quantity: 10m,
                Price: 20m,
                Proceeds: 200m,
                Commission: 0m,
                CommissionCurrency: "USD",
                RealizedPnL: 0m,
                Type: TradeType.Buy,
                Isin: "US2222222222"),
            new(
                Symbol: "ABC",
                Currency: "USD",
                DateTime: new DateTime(2025, 1, 4, 10, 0, 0),
                Quantity: 10m,
                Price: 30m,
                Proceeds: 300m,
                Commission: 0m,
                CommissionCurrency: "USD",
                RealizedPnL: 0m,
                Type: TradeType.Sell,
                Isin: "US2222222222")
        };

        var results = await calculator.CalculateAsync(
            new ParsedStatement(trades, [], []),
            TaxPeriod.FullYear(2025));
        var sellResult = Assert.Single(results, r => r.Type == TradeType.Sell);

        Assert.Equal(1500m, sellResult.ProceedsPln);
        Assert.Equal(1000m, sellResult.CostPln);
        Assert.Equal(500m, sellResult.GainLossPln);
    }

    [Fact]
    public async Task CalculateAsync_AppliesStockSplitToHistoricalLots()
    {
        var rateService = new TestRateService(new Dictionary<string, decimal>
        {
            ["USD"] = 1m
        });
        var calculator = new CapitalGainsTaxCalculator(rateService);

        var statement = new ParsedStatement(
            Trades:
            [
                new(
                    Symbol: "IBKR",
                    Currency: "USD",
                    DateTime: new DateTime(2024, 12, 20, 10, 0, 0),
                    Quantity: 1m,
                    Price: 100m,
                    Proceeds: 100m,
                    Commission: 0m,
                    CommissionCurrency: "USD",
                    RealizedPnL: 0m,
                    Type: TradeType.Buy,
                    Isin: "US45841N1072"),
                new(
                    Symbol: "IBKR",
                    Currency: "USD",
                    DateTime: new DateTime(2025, 9, 19, 9, 30, 1),
                    Quantity: 2m,
                    Price: 70m,
                    Proceeds: 140m,
                    Commission: 0m,
                    CommissionCurrency: "USD",
                    RealizedPnL: 0m,
                    Type: TradeType.Sell,
                    Isin: "US45841N1072")
            ],
            Dividends: [],
            WithholdingTaxes: [],
            CorporateActions:
            [
                new(
                    Symbol: "IBKR",
                    DateTime: new DateTime(2025, 6, 17, 20, 25, 0),
                    Type: CorporateActionType.StockSplit,
                    Numerator: 4m,
                    Denominator: 1m,
                    Isin: "US45841N1072")
            ]);

        var results = await calculator.CalculateAsync(statement, TaxPeriod.FullYear(2025));
        var sellResult = Assert.Single(results);

        Assert.Equal(TradeType.Sell, sellResult.Type);
        Assert.Equal(140m, sellResult.ProceedsPln);
        Assert.Equal(50m, sellResult.CostPln);
        Assert.Equal(90m, sellResult.GainLossPln);
    }

    [Fact]
    public async Task CalculateAsync_ExplainsWhenCarryInLotsAreMissing()
    {
        var rateService = new TestRateService(new Dictionary<string, decimal>
        {
            ["USD"] = 1m
        });
        var calculator = new CapitalGainsTaxCalculator(rateService);

        var statement = new ParsedStatement(
            Trades:
            [
                new(
                    Symbol: "IBKR",
                    Currency: "USD",
                    DateTime: new DateTime(2025, 9, 19, 9, 30, 1),
                    Quantity: 3.1m,
                    Price: 65.317741935m,
                    Proceeds: 202.485m,
                    Commission: 1.0005828m,
                    CommissionCurrency: "USD",
                    RealizedPnL: 139.859614m,
                    Type: TradeType.Sell,
                    Isin: "US45841N1072")
            ],
            Dividends: [],
            WithholdingTaxes: [],
            CorporateActions:
            [
                new(
                    Symbol: "IBKR",
                    DateTime: new DateTime(2025, 6, 17, 20, 25, 0),
                    Type: CorporateActionType.StockSplit,
                    Numerator: 4m,
                    Denominator: 1m,
                    Isin: "US45841N1072")
            ],
            CarryInPositions:
            [
                new("IBKR", 0.8847m, 2025, "US45841N1072")
            ]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => calculator.CalculateAsync(statement, TaxPeriod.FullYear(2025)));

        Assert.Contains("carried into 2025", ex.Message);
        Assert.Contains("prior-year CSVs", ex.Message);
    }
}
