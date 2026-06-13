namespace Pos.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "pos";
    public string Audience { get; set; } = "pos";
    public string SigningKey { get; set; } = "";   // symmetric key; supply via config/env in real use
    public int ExpiryMinutes { get; set; } = 60;
}
