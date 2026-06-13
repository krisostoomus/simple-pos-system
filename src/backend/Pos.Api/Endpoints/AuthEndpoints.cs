using Pos.Api.Contracts;
using Pos.Infrastructure.Auth;

namespace Pos.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapPost("/auth/token", (TokenRequest req, TokenService tokens) =>
        {
            var result = tokens.IssueStaffToken(req.Username, req.Password);
            return result is null
                ? Results.Json(new { errorCode = "invalid_credentials" }, statusCode: StatusCodes.Status401Unauthorized)
                : Results.Ok(new { accessToken = result.AccessToken, expiresAtUtc = result.ExpiresAtUtc });
        })
        .AllowAnonymous()
        .WithSummary("Exchange a staff credential for a JWT.");

        return group;
    }
}
