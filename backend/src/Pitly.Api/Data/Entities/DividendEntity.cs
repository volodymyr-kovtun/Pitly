namespace Pitly.Api.Data;

public class DividendEntity
{
    public int Id { get; set; }
    public Guid SessionId { get; set; }
    public string Symbol { get; set; } = "";
    public string Currency { get; set; } = "";
    public DateTime Date { get; set; }
    public decimal AmountOriginal { get; set; }
    public decimal WithholdingTaxOriginal { get; set; }
    public decimal AmountPln { get; set; }
    public decimal WithholdingTaxPln { get; set; }
    public decimal ExchangeRate { get; set; }
    public bool RateUnavailable { get; set; }
}
