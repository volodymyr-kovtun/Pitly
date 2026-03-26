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
            targetYear: 2025);
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

        var results = await calculator.CalculateAsync(statement, targetYear: 2025);
        var sellResult = Assert.Single(results);

        Assert.Equal(TradeType.Sell, sellResult.Type);
        Assert.Equal(140m, sellResult.ProceedsPln);
        Assert.Equal(50m, sellResult.CostPln);
        Assert.Equal(90m, sellResult.GainLossPln);
    }

    // Without the flag the calculator should still throw so the user is prompted
    // to either upload older CSVs or explicitly confirm the shares were gifted.
    [Fact]
    public async Task CalculateAsync_ThrowsForMissingLotsWhenFlagNotSet()
    {
        var rateService = new TestRateService(new Dictionary<string, decimal> { ["USD"] = 1m });
        var calculator = new CapitalGainsTaxCalculator(rateService);
        var statement = BuildIbkrGiftStatement(sellQty: 3.1m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => calculator.CalculateAsync(statement, targetYear: 2025));

        Assert.Contains("Cannot sell", ex.Message);
        Assert.Contains("prior-year CSVs", ex.Message);
    }

    // With assumeGiftedShares the calculator creates a PLN 0 synthetic lot and flags the result.
    [Fact]
    public async Task CalculateAsync_UsesSyntheticLotWhenGiftedFlagSet()
    {
        var rateService = new TestRateService(new Dictionary<string, decimal> { ["USD"] = 1m });
        var calculator = new CapitalGainsTaxCalculator(rateService);
        // 0.8847 carry-in × 4 (split) = 3.5388 synthetic shares — enough to cover the 3.1 sell
        var statement = BuildIbkrGiftStatement(sellQty: 3.1m);

        var results = await calculator.CalculateAsync(statement, targetYear: 2025, assumeGiftedShares: true);

        var sell = Assert.Single(results, r => r.Type == TradeType.Sell);
        Assert.Equal("IBKR", sell.Symbol);
        Assert.True(sell.HasEstimatedCost, "Sell that consumed a synthetic lot should be flagged");
        Assert.Equal(0m, sell.CostPln);
        Assert.True(sell.GainLossPln > 0);
    }

    // When the sell exceeds the synthetic lot (carry-in × split factor), it should still throw.
    [Fact]
    public async Task CalculateAsync_ThrowsWhenSyntheticLotIsAlsoInsufficient()
    {
        var rateService = new TestRateService(new Dictionary<string, decimal> { ["USD"] = 1m });
        var calculator = new CapitalGainsTaxCalculator(rateService);
        // 10 > 0.8847 × 4 = 3.5388
        var statement = BuildIbkrGiftStatement(sellQty: 10m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => calculator.CalculateAsync(statement, targetYear: 2025, assumeGiftedShares: true));

        Assert.Contains("IBKR", ex.Message);
    }

    private static ParsedStatement BuildIbkrGiftStatement(decimal sellQty) => new(
        Trades:
        [
            new(Symbol: "IBKR", Currency: "USD",
                DateTime: new DateTime(2025, 9, 19, 9, 30, 1),
                Quantity: sellQty, Price: 65.317741935m,
                Proceeds: sellQty * 65.317741935m, Commission: 1.0m,
                CommissionCurrency: "USD", RealizedPnL: 0m,
                Type: TradeType.Sell, Isin: "US45841N1072")
        ],
        Dividends: [],
        WithholdingTaxes: [],
        CorporateActions:
        [
            new(Symbol: "IBKR", DateTime: new DateTime(2025, 6, 17, 20, 25, 0),
                Type: CorporateActionType.StockSplit,
                Numerator: 4m, Denominator: 1m, Isin: "US45841N1072")
        ],
        CarryInPositions:
        [
            new("IBKR", 0.8847m, 2025, "US45841N1072")
        ]);
}
