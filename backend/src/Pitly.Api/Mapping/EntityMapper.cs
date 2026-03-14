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
            TotalProceedsPln = summary.TotalProceedsPln,
            TotalCostPln = summary.TotalCostPln,
            CapitalGainPln = summary.CapitalGainPln,
            CapitalGainTaxPln = summary.CapitalGainTaxPln,
            TotalDividendsPln = summary.TotalDividendsPln,
            TotalWithholdingPln = summary.TotalWithholdingPln,
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
                ExchangeRate = d.ExchangeRate,
                RateUnavailable = d.RateUnavailable
            }).ToList()
        };
    }

    public static object ToSummaryResponse(SessionEntity session)
    {
        return new
        {
            session.TotalProceedsPln,
            session.TotalCostPln,
            session.CapitalGainPln,
            session.CapitalGainTaxPln,
            session.TotalDividendsPln,
            session.TotalWithholdingPln,
            session.DividendTaxOwedPln,
            session.Year
        };
    }
}
