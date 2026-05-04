using Pitly.Api.Data;
using Pitly.Core.Models;

namespace Pitly.Api.Mapping;

public static class EntityMapper
{
    public static SessionEntity ToSessionEntity(TaxSummary summary)
    {
        return new SessionEntity
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Year = summary.Year,
            TaxableFrom = summary.TaxableFrom,
            TotalProceedsPln = summary.TotalProceedsPln,
            TotalCostPln = summary.TotalCostPln,
            CapitalGainPln = summary.CapitalGainPln,
            CapitalGainTaxPln = summary.CapitalGainTaxPln,
            TotalDividendsPln = summary.TotalDividendsPln,
            TotalWithholdingPln = summary.TotalWithholdingPln,
            TotalCreditableWithholdingPln = summary.TotalCreditableWithholdingPln,
            DividendTaxOwedPln = summary.DividendTaxOwedPln,
            TradeResults = summary.TradeResults.Select(t => new TradeResultEntity
            {
                Symbol = t.Symbol,
                DateTime = t.DateTime,
                Type = t.Type.ToString(),
                Quantity = t.Quantity,
                PriceOriginal = t.PriceOriginal,
                ProceedsOriginal = t.ProceedsOriginal,
                CommissionOriginal = t.CommissionOriginal,
                Currency = t.Currency,
                ExchangeRate = t.ExchangeRate,
                ProceedsPln = t.ProceedsPln,
                CostPln = t.CostPln,
                GainLossPln = t.GainLossPln,
                RateUnavailable = t.RateUnavailable
            }).ToList(),
            Dividends = summary.Dividends.Select(d => new DividendEntity
            {
                Symbol = d.Symbol,
                Currency = d.Currency,
                Date = d.Date,
                AmountOriginal = d.AmountOriginal,
                WithholdingTaxOriginal = d.WithholdingTaxOriginal,
                AmountPln = d.AmountPln,
                WithholdingTaxPln = d.WithholdingTaxPln,
                CreditableWithholdingTaxPln = d.CreditableWithholdingTaxPln,
                ExchangeRate = d.ExchangeRate,
                Isin = d.Isin,
                RateUnavailable = d.RateUnavailable
            }).ToList()
        };
    }

    public static TaxPeriod ToTaxPeriod(SessionEntity session)
    {
        var taxableFrom = session.TaxableFrom?.Date ?? new DateTime(session.Year, 1, 1);
        return new TaxPeriod(session.Year, taxableFrom, new DateTime(session.Year, 12, 31));
    }

    public static object ToSummaryResponse(SessionEntity session)
    {
        var taxPeriod = ToTaxPeriod(session);
        return new
        {
            session.TotalProceedsPln,
            session.TotalCostPln,
            session.CapitalGainPln,
            session.CapitalGainTaxPln,
            session.TotalDividendsPln,
            session.TotalWithholdingPln,
            session.TotalCreditableWithholdingPln,
            session.DividendTaxOwedPln,
            session.Year,
            taxPeriod.TaxableFrom,
            taxPeriod.TaxableTo
        };
    }
}
