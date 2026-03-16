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
            var file = form.Files.FirstOrDefault();

            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "No file uploaded" });

            try
            {
                var result = await importService.ImportStatementAsync(file.OpenReadStream());

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
                        result.Summary.DividendTaxOwedPln,
                        result.Summary.Year
                    },
                    trades = result.Summary.TradeResults,
                    dividends = result.Summary.Dividends
                });
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
