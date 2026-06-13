using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Pos.Infrastructure.Auth;

public sealed record TokenResult(string AccessToken, DateTime ExpiresAtUtc);

public sealed class TokenService(IOptions<JwtOptions> jwt, IOptions<StaffCredentialOptions> staff, TimeProvider clock)
{
    public const string StaffRole = "staff";

    /// <summary>Validates the supplied credential against the configured staff credential and,
    /// on success, issues a signed JWT carrying the staff role. Returns null on failure.</summary>
    public TokenResult? IssueStaffToken(string username, string password)
    {
        var expected = staff.Value;
        if (!string.Equals(username, expected.Username, StringComparison.Ordinal) ||
            !string.Equals(password, expected.Password, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(expected.Password))
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(jwt.Value.ExpiryMinutes);
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt.Value.Issuer,
            audience: jwt.Value.Audience,
            claims: [new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, StaffRole)],
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        return new TokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
