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

    public ImportService(IStatementParser parser, ITaxCalculator calculator, AppDbContext db)
    {
        _parser = parser;
        _calculator = calculator;
        _db = db;
    }

    public async Task<ImportResult> ImportStatementAsync(Stream fileStream)
    {
        using var reader = new StreamReader(fileStream);
        var content = await reader.ReadToEndAsync();

        var parsed = _parser.Parse(content);
        var summary = await _calculator.CalculateAsync(parsed);

        var session = EntityMapper.ToSessionEntity(summary);
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        return new ImportResult(session.Id, summary);
    }
}
