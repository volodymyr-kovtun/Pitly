namespace Pitly.Core.Tax;

/// <summary>
/// Per-country dividend withholding caps from Poland's bilateral tax treaties.
/// Used to clip the foreign-tax credit allowed by art. 30a ust. 9 ustawy o PIT.
/// </summary>
public static class TreatyRates
{
    /// <summary>
    /// 15% covers the vast majority of Polish dividend tax treaties (US, DE, FR, NL, ES, IT,
    /// AT, BE, CH, DK, NO, SE, FI, AU, CA, JP, KR, SG, ZA, MX, IN, IE, LU and others).
    /// </summary>
    public const decimal Default = 0.15m;

    private static readonly Dictionary<string, decimal> Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        // United Kingdom — DTT (2006), art. 10 ust. 2 lit. b: dividends generally 10%.
        ["GB"] = 0.10m,
    };

    /// <summary>
    /// Returns the dividend cap for the source country, falling back to <see cref="Default"/>.
    /// </summary>
    public static decimal ForCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length < 2)
            return Default;

        var key = countryCode.Substring(0, 2);
        return Overrides.TryGetValue(key, out var rate) ? rate : Default;
    }

    /// <summary>
    /// Returns the cap implied by the dividend's ISIN (first two letters = source country).
    /// </summary>
    public static decimal ForIsin(string? isin) => ForCountry(isin);
}
