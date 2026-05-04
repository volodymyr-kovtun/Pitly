using System.Globalization;
using Pitly.Api.Services;

namespace Pitly.Api.Endpoints;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        app.MapPost("/api/import", async (HttpRequest request, IImportService importService) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Expected multipart/form-data" });

            var form = await request.ReadFormAsync();
            DateTime? residencyStartDate = null;
            var residencyStartDateRaw = form["residencyStartDate"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(residencyStartDateRaw))
            {
                if (!DateTime.TryParseExact(
                        residencyStartDateRaw,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedResidencyStartDate))
                {
                    return Results.BadRequest(new
                    {
                        error = "Residency start date must use YYYY-MM-DD format."
                    });
                }

                residencyStartDate = parsedResidencyStartDate.Date;
            }

            var files = form.Files
                .Where(f => f.Length > 0)
                .ToList();

            if (files.Count == 0)
                return Results.BadRequest(new { error = "No file uploaded" });

            const long maxFileSize = 10 * 1024 * 1024; // 10 MB
            var oversizedFile = files.FirstOrDefault(file => file.Length > maxFileSize);
            if (oversizedFile is not null)
            {
                return Results.BadRequest(new
                {
                    error = $"File '{oversizedFile.FileName}' is too large ({oversizedFile.Length / 1024 / 1024} MB). Maximum allowed size is 10 MB."
                });
            }

            try
            {
                var streams = files.Select(file => file.OpenReadStream()).ToList();
                try
                {
                    var result = await importService.ImportStatementsAsync(streams, residencyStartDate);

                    return Results.Ok(new
                    {
                        sessionId = result.SessionId,
                        summary = new
                        {
                            result.Summary.TotalProceedsPln,
                            result.Summary.TotalCostPln,
                            result.Summary.CapitalGainPln,
                            result.Summary.CapitalGainTaxPln,
                            result.Summary.TotalDividendsPln,
                            result.Summary.TotalWithholdingPln,
                            result.Summary.TotalCreditableWithholdingPln,
                            result.Summary.DividendTaxOwedPln,
                            result.Summary.Year,
                            result.Summary.TaxableFrom,
                            result.Summary.TaxableTo
                        },
                        trades = result.Summary.TradeResults,
                        dividends = result.Summary.Dividends
                    });
                }
                finally
                {
                    foreach (var stream in streams)
                        stream.Dispose();
                }
            }
            catch (FormatException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).DisableAntiforgery();
    }
}
