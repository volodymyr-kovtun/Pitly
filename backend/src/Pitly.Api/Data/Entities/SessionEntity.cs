namespace Pitly.Api.Data;

public class SessionEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Year { get; set; }
    public DateTime? TaxableFrom { get; set; }
    public decimal TotalProceedsPln { get; set; }
    public decimal TotalCostPln { get; set; }
    public decimal CapitalGainPln { get; set; }
    public decimal CapitalGainTaxPln { get; set; }
    public decimal TotalDividendsPln { get; set; }
    public decimal TotalWithholdingPln { get; set; }
    public decimal DividendTaxOwedPln { get; set; }

    public List<TradeResultEntity> TradeResults { get; set; } = [];
    public List<DividendEntity> Dividends { get; set; } = [];
}
