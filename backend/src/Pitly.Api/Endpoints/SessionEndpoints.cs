using Pitly.Api.Data;
using Pitly.Api.Mapping;
using Pitly.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Pitly.Api.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/session/{sessionId:guid}");

        group.MapGet("/trades", async (Guid sessionId, AppDbContext db,
            int page = 1, int pageSize = 25, string? sortBy = null, string? sortOrder = null, string? symbolFilter = null) =>
        {
            var session = await db.Sessions.Include(s => s.TradeResults)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session is null)
                return Results.NotFound(new { error = "Session not found" });

            IEnumerable<TradeResultEntity> query = session.TradeResults;

            if (!string.IsNullOrEmpty(symbolFilter))
                query = query.Where(t => t.Symbol.Contains(symbolFilter, StringComparison.OrdinalIgnoreCase));

            query = (sortBy?.ToLowerInvariant(), sortOrder?.ToLowerInvariant()) switch
            {
                ("symbol", "desc") => query.OrderByDescending(t => t.Symbol),
                ("symbol", _) => query.OrderBy(t => t.Symbol),
                ("date", "desc") => query.OrderByDescending(t => t.DateTime),
                ("gainlosspln", "desc") => query.OrderByDescending(t => t.GainLossPln),
                ("gainlosspln", _) => query.OrderBy(t => t.GainLossPln),
                _ => query.OrderBy(t => t.DateTime)
            };

            var total = query.Count();
            var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Results.Ok(new { items, total, page, pageSize });
        });

        group.MapGet("/dividends", async (Guid sessionId, AppDbContext db) =>
        {
            var session = await db.Sessions.Include(s => s.Dividends)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session is null)
                return Results.NotFound(new { error = "Session not found" });

            return Results.Ok(session.Dividends);
        });

        group.MapGet("/summary", async (Guid sessionId, AppDbContext db) =>
        {
            var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session is null)
                return Results.NotFound(new { error = "Session not found" });

            return Results.Ok(EntityMapper.ToSummaryResponse(session));
        });

        group.MapGet("/pit38", async (Guid sessionId, AppDbContext db) =>
        {
            var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session is null)
                return Results.NotFound(new { error = "Session not found" });

            var taxPeriod = EntityMapper.ToTaxPeriod(session);
            // Sessions stored before #23 don't have a creditable column populated; fall back to
            // the actual withholding so legacy PIT-38 lookups keep their pre-fix behaviour.
            var creditable = session.TotalCreditableWithholdingPln > 0
                ? session.TotalCreditableWithholdingPln
                : session.TotalWithholdingPln;
            var summary = new TaxSummary(
                session.TotalProceedsPln, session.TotalCostPln,
                session.CapitalGainPln, session.CapitalGainTaxPln,
                session.TotalDividendsPln, session.TotalWithholdingPln,
                creditable,
                session.DividendTaxOwedPln, session.Year,
                taxPeriod.TaxableFrom, taxPeriod.TaxableTo,
                [], []);

            var pit38 = Pit38Fields.FromSummary(summary);
            return Results.Ok(pit38);
        });
    }
}
