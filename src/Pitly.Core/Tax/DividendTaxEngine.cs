using Pitly.Core.Models;
using Pitly.Core.Parsing;
using Pitly.Core.Services;

namespace Pitly.Core.Tax;

public class DividendTaxEngine
{
    private readonly INbpExchangeRateService _rateService;

    public DividendTaxEngine(INbpExchangeRateService rateService)
    {
        _rateService = rateService;
    }

    public async Task<List<Dividend>> CalculateAsync(
        List<RawDividend> rawDividends,
        List<RawWithholdingTax> rawWithholdingTaxes)
    {
        var results = new List<Dividend>();

        foreach (var div in rawDividends)
        {
            var matchingTax = rawWithholdingTaxes
                .FirstOrDefault(t => t.Symbol == div.Symbol && t.Date == div.Date);

            var withholdingAmount = matchingTax?.Amount ?? 0;

            decimal rate;
            bool rateUnavailable = false;
            try
            {
                rate = await _rateService.GetRateAsync(div.Currency, div.Date);
            }
            catch
            {
                rate = 0;
                rateUnavailable = true;
            }

            var amountPln = div.Amount * rate;
            var withholdingPln = withholdingAmount * rate;

            results.Add(new Dividend(
                Symbol: div.Symbol,
                Currency: div.Currency,
                Date: div.Date,
                AmountOriginal: div.Amount,
                WithholdingTaxOriginal: withholdingAmount,
                AmountPln: amountPln,
                WithholdingTaxPln: withholdingPln,
                ExchangeRate: rate,
                RateUnavailable: rateUnavailable));
        }

        return results;
    }
}
