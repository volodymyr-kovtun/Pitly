using System.Globalization;
using Pitly.Api.Services;
using Pitly.Core.Models;

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
            var files = form.Files
                .Where(f => f.Length > 0)
                .ToList();
            var assumeGiftedShares = form.TryGetValue("assumeGiftedShares", out var gifted)
                && gifted == "true";

            GiftedLotOverride? giftedLotOverride = null;
            if (assumeGiftedShares
                && form.TryGetValue("giftedSymbol", out var symVal) && !string.IsNullOrWhiteSpace(symVal)
                && form.TryGetValue("giftedDate", out var dateVal)
                && DateTime.TryParseExact(dateVal, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var grantDate)
                && form.TryGetValue("giftedPrice", out var priceVal)
                && decimal.TryParse(priceVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var grantPrice)
                && grantPrice > 0)
            {
                var currency = form.TryGetValue("giftedCurrency", out var cur) && !string.IsNullOrWhiteSpace(cur)
                    ? cur.ToString()
                    : "USD";
                giftedLotOverride = new GiftedLotOverride(symVal!, grantDate, grantPrice, currency);
            }

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
                    var result = await importService.ImportStatementsAsync(streams, assumeGiftedShares, giftedLotOverride);

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
