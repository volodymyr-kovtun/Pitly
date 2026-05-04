using Pitly.Core.Models;
using Pitly.Core.Tax;

namespace Pitly.Tests;

public class DividendTaxCalculatorTests
{
    [Fact]
    public async Task CalculateAsync_SumsMatchingWithholdingRowsForSameDividend()
    {
        var rateService = new TestRateService(new Dictionary<string, decimal>
        {
            ["USD"] = 4m
        });
        var calculator = new DividendTaxCalculator(rateService);

        var dividends = new List<RawDividend>
        {
            new("META", "USD", new DateTime(2025, 12, 23), 10m, "US30303M1027")
        };
        var withholdingTaxes = new List<RawWithholdingTax>
        {
            new("META", "USD", new DateTime(2025, 12, 23), 1.0m, "US30303M1027"),
            new("META", "USD", new DateTime(2025, 12, 23), 0.5m, "US30303M1027")
        };

        var results = await calculator.CalculateAsync(dividends, withholdingTaxes);
        var dividend = Assert.Single(results);

        Assert.Equal(10m, dividend.AmountOriginal);
        Assert.Equal(1.5m, dividend.WithholdingTaxOriginal);
        Assert.Equal(40m, dividend.AmountPln);
        Assert.Equal(6m, dividend.WithholdingTaxPln);
    }

    [Fact]
    public async Task CalculateAsync_DanishDividendCreditCappedAtTreatyRate()
    {
        // Denmark withholds 27% at source but PL-DK treaty caps the credit at 15%.
        var calculator = new DividendTaxCalculator(new TestRateService(new Dictionary<string, decimal>
        {
            ["USD"] = 4m,
        }));

        var dividends = new List<RawDividend>
        {
            new("NVO", "USD", new DateTime(2025, 8, 26), 100m, "DK0062498333"),
        };
        var withholdingTaxes = new List<RawWithholdingTax>
        {
            new("NVO", "USD", new DateTime(2025, 8, 26), 27m, "DK0062498333"),
        };

        var dividend = Assert.Single(await calculator.CalculateAsync(dividends, withholdingTaxes));

        Assert.Equal(400m, dividend.AmountPln);
        Assert.Equal(108m, dividend.WithholdingTaxPln);
        // Capped at 15% of gross PLN, not the 27% actually withheld.
        Assert.Equal(60m, dividend.CreditableWithholdingTaxPln);
        Assert.Equal("DK0062498333", dividend.Isin);
    }

    [Fact]
    public async Task CalculateAsync_BritishDividendCreditCappedAtTenPercent()
    {
        var calculator = new DividendTaxCalculator(new TestRateService(new Dictionary<string, decimal>
        {
            ["USD"] = 4m,
        }));

        var dividends = new List<RawDividend>
        {
            new("BP", "USD", new DateTime(2025, 6, 1), 100m, "GB0007980591"),
        };
        var withholdingTaxes = new List<RawWithholdingTax>
        {
            new("BP", "USD", new DateTime(2025, 6, 1), 15m, "GB0007980591"),
        };

        var dividend = Assert.Single(await calculator.CalculateAsync(dividends, withholdingTaxes));

        // PL-GB treaty caps credit at 10%, not the default 15%.
        Assert.Equal(40m, dividend.CreditableWithholdingTaxPln);
        Assert.Equal(60m, dividend.WithholdingTaxPln);
    }

    [Fact]
    public async Task CalculateAsync_UsDividendCreditUncappedWhenAtFifteenPercent()
    {
        var calculator = new DividendTaxCalculator(new TestRateService(new Dictionary<string, decimal>
        {
            ["USD"] = 4m,
        }));

        var dividends = new List<RawDividend>
        {
            new("META", "USD", new DateTime(2025, 12, 23), 100m, "US30303M1027"),
        };
        var withholdingTaxes = new List<RawWithholdingTax>
        {
            new("META", "USD", new DateTime(2025, 12, 23), 15m, "US30303M1027"),
        };

        var dividend = Assert.Single(await calculator.CalculateAsync(dividends, withholdingTaxes));

        // 15% withheld matches the PL-US treaty cap exactly — no clipping.
        Assert.Equal(60m, dividend.WithholdingTaxPln);
        Assert.Equal(60m, dividend.CreditableWithholdingTaxPln);
    }

    [Fact]
    public async Task CalculateAsync_MissingIsinFallsBackToDefaultTreatyCap()
    {
        var calculator = new DividendTaxCalculator(new TestRateService(new Dictionary<string, decimal>
        {
            ["USD"] = 4m,
        }));

        var dividends = new List<RawDividend>
        {
            new("X", "USD", new DateTime(2025, 5, 1), 100m, null),
        };
        var withholdingTaxes = new List<RawWithholdingTax>
        {
            new("X", "USD", new DateTime(2025, 5, 1), 25m, null),
        };

        var dividend = Assert.Single(await calculator.CalculateAsync(dividends, withholdingTaxes));

        // Without an ISIN we fall back to the 15% default cap.
        Assert.Equal(100m, dividend.WithholdingTaxPln);
        Assert.Equal(60m, dividend.CreditableWithholdingTaxPln);
    }

    [Fact]
    public void TreatyRates_ReturnsTenPercentForGb_FifteenForUnknownAndShortInputs()
    {
        Assert.Equal(0.10m, TreatyRates.ForCountry("GB"));
        Assert.Equal(0.10m, TreatyRates.ForIsin("GB0007980591"));
        Assert.Equal(0.15m, TreatyRates.ForCountry("US"));
        Assert.Equal(0.15m, TreatyRates.ForCountry("CN"));   // No override: defaults to 15%.
        Assert.Equal(0.15m, TreatyRates.ForCountry(null));
        Assert.Equal(0.15m, TreatyRates.ForCountry(""));
        Assert.Equal(0.15m, TreatyRates.ForCountry("U"));
    }
}
