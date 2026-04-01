using Pitly.Api.Data;
using Pitly.Api.Mapping;
using Pitly.Core.Models;
using Pitly.Core.Parsing;
using Pitly.Core.Tax;

namespace Pitly.Api.Services;

public class ImportService : IImportService
{
    private readonly IStatementParser _parser;
    private readonly ITaxCalculator _calculator;
    private readonly AppDbContext _db;
    private readonly ILogger<ImportService> _logger;

    public ImportService(IStatementParser parser, ITaxCalculator calculator, AppDbContext db, ILogger<ImportService> logger)
    {
        _parser = parser;
        _calculator = calculator;
        _db = db;
        _logger = logger;
    }

    public async Task<ImportResult> ImportStatementsAsync(IReadOnlyList<Stream> fileStreams, DateTime? residencyStartDate = null)
    {
        if (fileStreams.Count == 0)
            throw new FormatException("No files uploaded.");

        var statements = new List<ParsedStatement>();

        for (var i = 0; i < fileStreams.Count; i++)
        {
            using var reader = new StreamReader(fileStreams[i], leaveOpen: true);
            var content = await reader.ReadToEndAsync();

            _logger.LogInformation("Parsing statement {Index}/{Count} ({Length} chars)", i + 1, fileStreams.Count, content.Length);
            var parsed = _parser.Parse(content);
            statements.Add(parsed);
            _logger.LogInformation(
                "Parsed statement {Index}/{Count}: {Trades} trades, {Dividends} dividends, {Taxes} withholding taxes, {Actions} corporate actions",
                i + 1,
                fileStreams.Count,
                parsed.Trades.Count,
                parsed.Dividends.Count,
                parsed.WithholdingTaxes.Count,
                parsed.CorporateActions?.Count ?? 0);
        }

        var taxPeriod = DetermineTaxPeriod(statements, residencyStartDate);
        var merged = MergeStatements(statements, taxPeriod.Year);

        var summary = await _calculator.CalculateAsync(merged, taxPeriod);
        _logger.LogInformation(
            "Tax calculation complete for year {Year} from {TaxableFrom}: capital gain {Gain} PLN, dividend tax owed {DivTax} PLN",
            summary.Year,
            summary.TaxableFrom.ToString("yyyy-MM-dd"),
            summary.CapitalGainPln,
            summary.DividendTaxOwedPln);

        var session = EntityMapper.ToSessionEntity(summary);
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Session {SessionId} saved", session.Id);
        return new ImportResult(session.Id, summary);
    }

    private static int DetermineTargetYear(IReadOnlyList<ParsedStatement> statements)
    {
        var years = statements
            .Select(GetStatementYear)
            .ToList();

        if (years.Count == 0)
        {
            throw new FormatException(
                "The uploaded files contain no trades or dividends. Please check that the CSV export is valid.");
        }

        return years.Max();
    }

    private static TaxPeriod DetermineTaxPeriod(
        IReadOnlyList<ParsedStatement> statements,
        DateTime? residencyStartDate)
    {
        var targetYear = DetermineTargetYear(statements);
        if (residencyStartDate is null)
            return TaxPeriod.FullYear(targetYear);

        var taxableFrom = residencyStartDate.Value.Date;
        if (taxableFrom.Year != targetYear)
        {
            throw new FormatException(
                $"Residency start date {taxableFrom:yyyy-MM-dd} must fall within the imported tax year {targetYear}.");
        }

        return new TaxPeriod(targetYear, taxableFrom, new DateTime(targetYear, 12, 31));
    }

    private static int GetStatementYear(ParsedStatement statement)
    {
        if (statement.StatementYear is not null)
            return statement.StatementYear.Value;

        var years = statement.Trades.Select(t => t.DateTime.Year)
            .Concat(statement.Dividends.Select(d => d.Date.Year))
            .Concat(statement.WithholdingTaxes.Select(t => t.Date.Year))
            .Concat((statement.CorporateActions ?? []).Select(a => a.DateTime.Year))
            .Concat((statement.CarryInPositions ?? []).Select(p => p.Year))
            .ToList();

        if (years.Count == 0)
        {
            throw new FormatException(
                "The uploaded files contain no trades or dividends. Please check that the CSV export is valid.");
        }

        return years.Max();
    }

    private static ParsedStatement MergeStatements(IReadOnlyList<ParsedStatement> statements, int targetYear)
    {
        return new ParsedStatement(
            Trades: statements.SelectMany(s => s.Trades).OrderBy(t => t.DateTime).ToList(),
            Dividends: statements.SelectMany(s => s.Dividends).OrderBy(d => d.Date).ToList(),
            WithholdingTaxes: statements.SelectMany(s => s.WithholdingTaxes).OrderBy(t => t.Date).ToList(),
            CorporateActions: statements.SelectMany(s => s.CorporateActions ?? []).OrderBy(a => a.DateTime).ToList(),
            CarryInPositions: statements.SelectMany(s => s.CarryInPositions ?? []).ToList(),
            StatementYear: targetYear);
    }
}
