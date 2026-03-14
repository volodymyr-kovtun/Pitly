namespace Pitly.Core.Services;

public interface INbpExchangeRateService
{
    Task<decimal> GetRateAsync(string currency, DateTime transactionDate);
}
