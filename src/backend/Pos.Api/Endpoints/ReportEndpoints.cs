using Pos.Application.Reporting;

namespace Pos.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/reports/summary", async (ReportingService reporting, HttpContext http, CancellationToken ct) =>
        {
            var culture = http.Request.Headers.AcceptLanguage.ToString();
            return Results.Ok(await reporting.GetSummaryAsync(culture, ct));
        })
        .RequireAuthorization("staff")
        .WithSummary("Funds raised and items sold (staff only).");

        return group;
    }
}
