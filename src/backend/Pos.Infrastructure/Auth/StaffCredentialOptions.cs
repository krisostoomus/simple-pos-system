namespace Pos.Infrastructure.Auth;

public sealed class StaffCredentialOptions
{
    public const string SectionName = "StaffCredential";
    public string Username { get; set; } = "staff";
    public string Password { get; set; } = "";
}
