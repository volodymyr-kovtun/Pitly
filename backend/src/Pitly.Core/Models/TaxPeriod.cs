namespace Pitly.Core.Models;

public sealed record TaxPeriod
{
    public int Year { get; }
    public DateTime TaxableFrom { get; }
    public DateTime TaxableTo { get; }

    public TaxPeriod(int year, DateTime taxableFrom, DateTime taxableTo)
    {
        Year = year;
        TaxableFrom = taxableFrom.Date;
        TaxableTo = taxableTo.Date;

        if (TaxableFrom.Year != year || TaxableTo.Year != year)
            throw new ArgumentOutOfRangeException(nameof(taxableFrom), "Tax period dates must stay within a single tax year.");

        if (TaxableFrom > TaxableTo)
            throw new ArgumentOutOfRangeException(nameof(taxableFrom), "Tax period start date cannot be later than the end date.");
    }

    public static TaxPeriod FullYear(int year)
        => new(year, new DateTime(year, 1, 1), new DateTime(year, 12, 31));

    public bool IncludesDate(DateTime date)
    {
        var day = date.Date;
        return day >= TaxableFrom && day <= TaxableTo;
    }
}
