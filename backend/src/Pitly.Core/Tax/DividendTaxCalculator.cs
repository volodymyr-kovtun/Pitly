using Pitly.Core.Models;
using Pitly.Core.Services;

namespace Pitly.Core.Tax;

public interface IDividendTaxCalculator
{
    Task<List<Dividend>> CalculateAsync(List<RawDividend> dividends, List<RawWithholdingTax> withholdingTaxes);
}

public class DividendTaxCalculator : IDividendTaxCalculator
{
    private readonly INbpExchangeRateService _rateService;

    public DividendTaxCalculator(INbpExchangeRateService rateService)
    {
        _rateService = rateService;
    }

    public async Task<List<Dividend>> CalculateAsync(List<RawDividend> rawDividends, List<RawWithholdingTax> rawWithholdingTaxes)
    {
        var results = new List<Dividend>();

        foreach (var div in rawDividends)
        {
            var rate = await _rateService.GetRateAsync(div.Currency, div.Date);
            var amountPln = div.Amount * rate;
            var matchingTaxes = rawWithholdingTaxes
                .Where(t => IsMatch(div, t))
                .ToList();

            var withholdingAmount = matchingTaxes.Sum(t => t.Amount);
            var withholdingCurrencies = matchingTaxes
                .Select(t => t.Currency)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (withholdingCurrencies.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Dividend withholding tax for {div.Symbol} on {div.Date:yyyy-MM-dd} uses multiple currencies. " +
                    "This Trading 212 export is not supported safely.");
            }

            decimal withholdingPln = 0;
            foreach (var tax in matchingTaxes)
            {
                if (tax.Amount == 0)
                    continue;

                var withholdingRate = tax.Currency.Equals(div.Currency, StringComparison.OrdinalIgnoreCase)
                    ? rate
                    : await _rateService.GetRateAsync(tax.Currency, div.Date);

                withholdingPln += tax.Amount * withholdingRate;
            }

            // Per art. 30a ust. 9 ustawy o PIT, the foreign-tax credit cannot exceed the rate
            // set in the bilateral tax treaty with the dividend's source country. The source
            // country comes from the first two letters of the ISIN.
            var treatyRate = TreatyRates.ForIsin(div.Isin);
            var treatyCap = amountPln * treatyRate;
            var creditablePln = Math.Min(withholdingPln, treatyCap);

            results.Add(new Dividend(
                Symbol: div.Symbol,
                Currency: div.Currency,
                Date: div.Date,
                AmountOriginal: div.Amount,
                WithholdingTaxOriginal: withholdingAmount,
                AmountPln: amountPln,
                WithholdingTaxPln: withholdingPln,
                CreditableWithholdingTaxPln: creditablePln,
                ExchangeRate: rate));
        }

        return results;
    }

    private static bool IsMatch(RawDividend dividend, RawWithholdingTax tax)
    {
        if (dividend.Date != tax.Date)
            return false;

        if (!string.IsNullOrWhiteSpace(dividend.Isin) || !string.IsNullOrWhiteSpace(tax.Isin))
            return string.Equals(dividend.Isin, tax.Isin, StringComparison.OrdinalIgnoreCase);

        return string.Equals(dividend.Symbol, tax.Symbol, StringComparison.OrdinalIgnoreCase);
    }
}
