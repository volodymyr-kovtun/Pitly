using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Pitly.Api.Data;
using Pitly.Core.Models;
using Pitly.Core.Parsing;
using Pitly.Core.Services;
using Pitly.Core.Tax;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=pitly.db"));

builder.Services.AddHttpClient<INbpExchangeRateService, NbpExchangeRateService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();

app.MapPost("/api/import", async (HttpRequest request, AppDbContext db, INbpExchangeRateService rateService) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Expected multipart/form-data" });

    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();

    if (file is null || file.Length == 0)
        return Results.BadRequest(new { error = "No file uploaded" });

    string csvContent;
    using (var reader = new StreamReader(file.OpenReadStream()))
    {
        csvContent = await reader.ReadToEndAsync();
    }

    ParsedStatement parsed;
    try
    {
        parsed = IbActivityParser.Parse(csvContent);
    }
    catch (FormatException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    var calculator = new TaxCalculator(rateService);
    var summary = await calculator.CalculateAsync(parsed);

    var sessionId = Guid.NewGuid();
    var session = new SessionEntity
    {
        Id = sessionId,
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

    db.Sessions.Add(session);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        sessionId,
        summary = new
        {
            summary.TotalProceedsPln,
            summary.TotalCostPln,
            summary.CapitalGainPln,
            summary.CapitalGainTaxPln,
            summary.TotalDividendsPln,
            summary.TotalWithholdingPln,
            summary.DividendTaxOwedPln,
            summary.Year
        },
        trades = summary.TradeResults,
        dividends = summary.Dividends
    });
}).DisableAntiforgery();

app.MapGet("/api/session/{sessionId}/trades", async (Guid sessionId, AppDbContext db,
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

app.MapGet("/api/session/{sessionId}/dividends", async (Guid sessionId, AppDbContext db) =>
{
    var session = await db.Sessions.Include(s => s.Dividends)
        .FirstOrDefaultAsync(s => s.Id == sessionId);

    if (session is null)
        return Results.NotFound(new { error = "Session not found" });

    return Results.Ok(session.Dividends);
});

app.MapGet("/api/session/{sessionId}/summary", async (Guid sessionId, AppDbContext db) =>
{
    var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);

    if (session is null)
        return Results.NotFound(new { error = "Session not found" });

    return Results.Ok(new
    {
        session.TotalProceedsPln,
        session.TotalCostPln,
        session.CapitalGainPln,
        session.CapitalGainTaxPln,
        session.TotalDividendsPln,
        session.TotalWithholdingPln,
        session.DividendTaxOwedPln,
        session.Year
    });
});

app.MapGet("/api/session/{sessionId}/pit38", async (Guid sessionId, AppDbContext db) =>
{
    var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);

    if (session is null)
        return Results.NotFound(new { error = "Session not found" });

    var summary = new TaxSummary(
        session.TotalProceedsPln, session.TotalCostPln,
        session.CapitalGainPln, session.CapitalGainTaxPln,
        session.TotalDividendsPln, session.TotalWithholdingPln,
        session.DividendTaxOwedPln, session.Year, [], []);

    var pit38 = TaxCalculator.BuildPit38(summary);
    return Results.Ok(pit38);
});

app.MapGet("/api/session/{sessionId}/export/csv", async (Guid sessionId, AppDbContext db) =>
{
    var session = await db.Sessions
        .Include(s => s.TradeResults)
        .Include(s => s.Dividends)
        .FirstOrDefaultAsync(s => s.Id == sessionId);

    if (session is null)
        return Results.NotFound(new { error = "Session not found" });

    var sb = new StringBuilder();
    sb.AppendLine("Section,Date,Symbol,Type,Quantity,Price,Proceeds (Original),Commission,Currency,Exchange Rate,Proceeds (PLN),Cost (PLN),Gain/Loss (PLN)");

    foreach (var t in session.TradeResults.OrderBy(t => t.DateTime))
    {
        sb.AppendLine(string.Join(",",
            "Trade",
            t.DateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            t.Symbol, t.Type,
            t.Quantity.ToString(CultureInfo.InvariantCulture),
            t.PriceOriginal.ToString(CultureInfo.InvariantCulture),
            t.ProceedsOriginal.ToString(CultureInfo.InvariantCulture),
            t.CommissionOriginal.ToString(CultureInfo.InvariantCulture),
            t.Currency,
            t.ExchangeRate.ToString("F4", CultureInfo.InvariantCulture),
            t.ProceedsPln.ToString("F2", CultureInfo.InvariantCulture),
            t.CostPln.ToString("F2", CultureInfo.InvariantCulture),
            t.GainLossPln.ToString("F2", CultureInfo.InvariantCulture)));
    }

    sb.AppendLine();
    sb.AppendLine("Section,Date,Symbol,Amount (Original),Withholding (Original),Currency,Exchange Rate,Amount (PLN),Withholding (PLN)");

    foreach (var d in session.Dividends.OrderBy(d => d.Date))
    {
        sb.AppendLine(string.Join(",",
            "Dividend",
            d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            d.Symbol,
            d.AmountOriginal.ToString(CultureInfo.InvariantCulture),
            d.WithholdingTaxOriginal.ToString(CultureInfo.InvariantCulture),
            d.Currency,
            d.ExchangeRate.ToString("F4", CultureInfo.InvariantCulture),
            d.AmountPln.ToString("F2", CultureInfo.InvariantCulture),
            d.WithholdingTaxPln.ToString("F2", CultureInfo.InvariantCulture)));
    }

    return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"ib-tax-{session.Year}.csv");
});

app.Run();
