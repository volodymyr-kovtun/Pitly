using System.Text.Json.Serialization;
using Pitly.Api.Data;
using Pitly.Api.Endpoints;
using Pitly.Api.Services;
using Pitly.Broker.InteractiveBrokers;
using Pitly.Broker.Trading212;
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
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient("Nbp");
builder.Services.AddScoped<INbpExchangeRateService>(sp =>
    new NbpExchangeRateService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("Nbp"),
        sp.GetRequiredService<ILogger<NbpExchangeRateService>>()));

builder.Services.AddScoped<InteractiveBrokersStatementParser>();
builder.Services.AddScoped<Trading212StatementParser>();
builder.Services.AddScoped<IStatementParser, StatementParserDispatcher>();
builder.Services.AddScoped<ICapitalGainsTaxCalculator, CapitalGainsTaxCalculator>();
builder.Services.AddScoped<IDividendTaxCalculator, DividendTaxCalculator>();
builder.Services.AddScoped<ITaxCalculator, TaxCalculator>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddHostedService<SessionCleanupService>();

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
    // Add columns introduced after initial schema creation (safe to run on existing DBs)
    try { db.Database.ExecuteSqlRaw("ALTER TABLE TradeResults ADD COLUMN HasEstimatedCost INTEGER NOT NULL DEFAULT 0"); }
    catch { /* column already exists */ }
}

app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));

app.MapImportEndpoints();
app.MapSessionEndpoints();
app.MapExportEndpoints();

app.Run();
