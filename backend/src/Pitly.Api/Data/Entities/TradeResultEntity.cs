namespace Pitly.Api.Data;

public class TradeResultEntity
{
    public int Id { get; set; }
    public Guid SessionId { get; set; }
    public string Symbol { get; set; } = "";
    public DateTime DateTime { get; set; }
    public string Type { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal PriceOriginal { get; set; }
    public decimal ProceedsOriginal { get; set; }
    public decimal CommissionOriginal { get; set; }
    public string Currency { get; set; } = "";
    public decimal ExchangeRate { get; set; }
    public decimal ProceedsPln { get; set; }
    public decimal CostPln { get; set; }
    public decimal GainLossPln { get; set; }
    public bool RateUnavailable { get; set; }
    public bool HasEstimatedCost { get; set; }
}
