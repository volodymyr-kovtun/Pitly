using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Pitly.Core.Services;

public class NbpExchangeRateService : INbpExchangeRateService
{
    private static readonly SemaphoreSlim Throttle = new(2, 2);

    private readonly HttpClient _httpClient;
    private readonly ILogger<NbpExchangeRateService> _logger;
    private readonly ConcurrentDictionary<string, decimal> _cache = new();

    public NbpExchangeRateService(HttpClient httpClient, ILogger<NbpExchangeRateService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<decimal> GetRateAsync(string currency, DateTime transactionDate)
    {
        if (currency.Equals("PLN", StringComparison.OrdinalIgnoreCase))
            return 1m;
        const int maxAttempts = 10;

        // Polish tax law: rate from last business day BEFORE the transaction date
        var rateDate = transactionDate.Date.AddDays(-1);
        Exception? lastError = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var dateStr = rateDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var cacheKey = $"{currency.ToUpperInvariant()}_{dateStr}";

            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            HttpResponseMessage? response = null;
            Exception? transportError = null;
            await Throttle.WaitAsync();
            try
            {
                var url = $"https://api.nbp.pl/api/exchangerates/rates/A/{currency.ToUpperInvariant()}/{dateStr}/?format=json";
                response = await _httpClient.GetAsync(url);
            }
            // HttpClient.Timeout surfaces as TaskCanceledException; treat it like a transport error.
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                transportError = ex;
            }
            finally
            {
                Throttle.Release();
            }

            if (transportError is not null)
            {
                lastError = transportError;
                _logger.LogWarning(transportError,
                    "NBP API transport error for {Currency} on {Date} (attempt {Attempt}/{MaxAttempts}), backing off",
                    currency, dateStr, attempt + 1, maxAttempts);
                await Task.Delay(BackoffDelay(attempt));
                continue;
            }

            using (response!)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogDebug("NBP rate not found for {Currency} on {Date}, trying previous day", currency, dateStr);
                    rateDate = rateDate.AddDays(-1);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                {
                    var status = response.StatusCode;
                    lastError = new HttpRequestException($"NBP API returned {(int)status} {status}.");
                    _logger.LogWarning(
                        "NBP API throttled/unavailable ({Status}) for {Currency} on {Date} (attempt {Attempt}/{MaxAttempts}), backing off",
                        (int)status, currency, dateStr, attempt + 1, maxAttempts);
                    await Task.Delay(BackoffDelay(attempt));
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var rate = doc.RootElement
                    .GetProperty("rates")[0]
                    .GetProperty("mid")
                    .GetDecimal();

                _cache.TryAdd(cacheKey, rate);
                _logger.LogDebug("NBP rate for {Currency} on {Date}: {Rate}", currency, dateStr, rate);
                return rate;
            }
        }

        _logger.LogError(lastError,
            "Failed to get NBP rate for {Currency} near {Date} after {MaxAttempts} attempts",
            currency, transactionDate, maxAttempts);
        throw new InvalidOperationException(
            $"Could not retrieve NBP exchange rate for {currency} near {transactionDate:yyyy-MM-dd} after {maxAttempts} attempts. " +
            "The NBP API may be rate-limiting or temporarily unavailable; please retry shortly.",
            lastError);
    }

    private static TimeSpan BackoffDelay(int attempt)
    {
        var capped = Math.Min(attempt, 4);
        var baseMs = 500 * (1 << capped);
        var jitter = Random.Shared.Next(0, 250);
        return TimeSpan.FromMilliseconds(baseMs + jitter);
    }
}
