using Microsoft.EntityFrameworkCore;

namespace Pitly.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<TradeResultEntity> TradeResults => Set<TradeResultEntity>();
    public DbSet<DividendEntity> Dividends => Set<DividendEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionEntity>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasMany(s => s.TradeResults).WithOne().HasForeignKey(t => t.SessionId);
            e.HasMany(s => s.Dividends).WithOne().HasForeignKey(d => d.SessionId);
        });
    }
}

public class SessionEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Year { get; set; }
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
}

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
