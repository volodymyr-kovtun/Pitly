using Pitly.Core.Models;
using Pitly.Core.Tax;

namespace Pitly.Tests;

public class TaxCalculatorTests
{
    [Fact]
    public async Task CalculateAsync_UsesHistoricalTradesButOnlyCountsTargetYear()
    {
        var rateService = new TestRateService(new Dictionary<string, decimal>
        {
            ["USD"] = 1m
        });
        var calculator = new TaxCalculator(
            new CapitalGainsTaxCalculator(rateService),
            new DividendTaxCalculator(rateService));

        var statement = new ParsedStatement(
            Trades:
            [
                new(
                    Symbol: "ABC",
                    Currency: "USD",
                    DateTime: new DateTime(2024, 12, 20, 10, 0, 0),
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
                    DateTime: new DateTime(2025, 3, 10, 10, 0, 0),
                    Quantity: 5m,
                    Price: 20m,
                    Proceeds: 100m,
                    Commission: 0m,
                    CommissionCurrency: "USD",
                    RealizedPnL: 0m,
                    Type: TradeType.Sell,
                    Isin: "US1111111111")
            ],
            Dividends:
            [
                new("ABC", "USD", new DateTime(2024, 6, 1), 1m, "US1111111111"),
                new("ABC", "USD", new DateTime(2025, 6, 1), 2m, "US1111111111")
            ],
            WithholdingTaxes:
            [
                new("ABC", "USD", new DateTime(2024, 6, 1), 0.1m, "US1111111111"),
                new("ABC", "USD", new DateTime(2025, 6, 1), 0.3m, "US1111111111")
            ]);

        var summary = await calculator.CalculateAsync(statement, TaxPeriod.FullYear(2025));

        Assert.Equal(2025, summary.Year);
        Assert.Equal(new DateTime(2025, 1, 1), summary.TaxableFrom);
        Assert.Equal(new DateTime(2025, 12, 31), summary.TaxableTo);
        Assert.Equal(100m, summary.TotalProceedsPln);
        Assert.Equal(50m, summary.TotalCostPln);
        Assert.Equal(50m, summary.CapitalGainPln);
        Assert.Equal(9.5m, summary.CapitalGainTaxPln);
        Assert.Equal(2m, summary.TotalDividendsPln);
        Assert.Equal(0.3m, summary.TotalWithholdingPln);
        Assert.Equal(0.08m, summary.DividendTaxOwedPln);

        var sell = Assert.Single(summary.TradeResults);
        Assert.Equal(TradeType.Sell, sell.Type);
        Assert.Single(summary.Dividends);
    }

    [Fact]
    public async Task CalculateAsync_ReplaysPreResidencyHistoryButReportsOnlyTaxableWindow()
    {
        var rateService = new TestRateService(new Dictionary<string, decimal>
        {
            ["USD"] = 1m
        });
        var calculator = new TaxCalculator(
            new CapitalGainsTaxCalculator(rateService),
            new DividendTaxCalculator(rateService));

        var statement = new ParsedStatement(
            Trades:
            [
                new(
                    Symbol: "ABC",
                    Currency: "USD",
                    DateTime: new DateTime(2025, 1, 10, 10, 0, 0),
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
                    DateTime: new DateTime(2025, 3, 15, 10, 0, 0),
                    Quantity: 5m,
                    Price: 12m,
                    Proceeds: 60m,
                    Commission: 0m,
                    CommissionCurrency: "USD",
                    RealizedPnL: 0m,
                    Type: TradeType.Buy,
                    Isin: "US1111111111"),
                new(
                    Symbol: "ABC",
                    Currency: "USD",
                    DateTime: new DateTime(2025, 4, 2, 10, 0, 0),
                    Quantity: 8m,
                    Price: 15m,
                    Proceeds: 120m,
                    Commission: 0m,
                    CommissionCurrency: "USD",
                    RealizedPnL: 0m,
                    Type: TradeType.Sell,
                    Isin: "US1111111111"),
                new(
                    Symbol: "ABC",
                    Currency: "USD",
                    DateTime: new DateTime(2025, 7, 8, 10, 0, 0),
                    Quantity: 7m,
                    Price: 20m,
                    Proceeds: 140m,
                    Commission: 0m,
                    CommissionCurrency: "USD",
                    RealizedPnL: 0m,
                    Type: TradeType.Sell,
                    Isin: "US1111111111")
            ],
            Dividends:
            [
                new("ABC", "USD", new DateTime(2025, 4, 5), 1m, "US1111111111"),
                new("ABC", "USD", new DateTime(2025, 8, 5), 2m, "US1111111111")
            ],
            WithholdingTaxes:
            [
                new("ABC", "USD", new DateTime(2025, 4, 5), 0.15m, "US1111111111"),
                new("ABC", "USD", new DateTime(2025, 8, 5), 0.3m, "US1111111111")
            ]);

        var summary = await calculator.CalculateAsync(
            statement,
            new TaxPeriod(2025, new DateTime(2025, 6, 1), new DateTime(2025, 12, 31)));

        Assert.Equal(2025, summary.Year);
        Assert.Equal(new DateTime(2025, 6, 1), summary.TaxableFrom);
        Assert.Equal(new DateTime(2025, 12, 31), summary.TaxableTo);
        Assert.Equal(140m, summary.TotalProceedsPln);
        Assert.Equal(80m, summary.TotalCostPln);
        Assert.Equal(60m, summary.CapitalGainPln);
        Assert.Equal(11.4m, summary.CapitalGainTaxPln);
        Assert.Equal(2m, summary.TotalDividendsPln);
        Assert.Equal(0.3m, summary.TotalWithholdingPln);
        Assert.Equal(0.08m, summary.DividendTaxOwedPln);

        var sell = Assert.Single(summary.TradeResults);
        Assert.Equal(TradeType.Sell, sell.Type);
        Assert.Equal(new DateTime(2025, 7, 8, 10, 0, 0), sell.DateTime);

        var dividend = Assert.Single(summary.Dividends);
        Assert.Equal(new DateTime(2025, 8, 5), dividend.Date);
    }
}
