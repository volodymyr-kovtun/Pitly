using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Pitly.Core.Services;

public interface INbpExchangeRateService
{
    Task<decimal> GetRateAsync(string currency, DateTime transactionDate);
}

public class NbpExchangeRateService : INbpExchangeRateService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, decimal> _cache = new();

    public NbpExchangeRateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<decimal> GetRateAsync(string currency, DateTime transactionDate)
    {
        if (currency.Equals("PLN", StringComparison.OrdinalIgnoreCase))
            return 1m;

        // Polish tax law: rate from last business day BEFORE the transaction date
        var rateDate = transactionDate.Date.AddDays(-1);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            var dateStr = rateDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var cacheKey = $"{currency.ToUpperInvariant()}_{dateStr}";

            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            try
            {
                var url = $"https://api.nbp.pl/api/exchangerates/rates/A/{currency.ToUpperInvariant()}/{dateStr}/?format=json";
                var response = await _httpClient.GetAsync(url);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    rateDate = rateDate.AddDays(-1);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var rate = doc.RootElement
                    .GetProperty("rates")[0]
                    .GetProperty("mid")
                    .GetDecimal();

                _cache[cacheKey] = rate;
                return rate;
            }
            catch (HttpRequestException) when (attempt < 4)
            {
                rateDate = rateDate.AddDays(-1);
            }
        }

        throw new InvalidOperationException(
            $"Could not find NBP exchange rate for {currency} near {transactionDate:yyyy-MM-dd} after 5 attempts.");
    }
}
