using Microsoft.AspNetCore.Mvc;
using Pos.Application.Reporting;
using Pos.Infrastructure.Auth;

namespace Pos.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/reports/summary", async (ReportingService reporting, [FromHeader(Name = "Accept-Language")] string? acceptLanguage, CancellationToken ct) =>
            Results.Ok(await reporting.GetSummaryAsync(acceptLanguage, ct)))
        .RequireAuthorization(TokenService.StaffRole)
        .WithSummary("Funds raised and items sold (staff only).");

        return group;
    }
}
