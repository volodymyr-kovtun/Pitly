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
