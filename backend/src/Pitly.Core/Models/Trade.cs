namespace Pitly.Core.Models;

public record Trade(
    string Symbol,
    string Currency,
    DateTime DateTime,
    decimal Quantity,
    decimal Price,
    decimal Proceeds,
    decimal Commission,
    string CommissionCurrency,
    decimal RealizedPnL,
    TradeType Type,
    string? Isin = null);

public enum TradeType { Buy, Sell }

public record TradeResult(
    string Symbol,
    DateTime DateTime,
    TradeType Type,
    decimal Quantity,
    decimal PriceOriginal,
    decimal ProceedsOriginal,
    decimal CommissionOriginal,
    string Currency,
    decimal ExchangeRate,
    decimal ProceedsPln,
    decimal CostPln,
    decimal GainLossPln,
    bool RateUnavailable = false,
    bool HasEstimatedCost = false);
