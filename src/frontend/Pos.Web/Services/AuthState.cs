namespace Pos.Web.Services;

/// <summary>Holds the staff JWT for the current session (admin actions only).</summary>
public sealed class AuthState
{
    public string? Token { get; private set; }
    public bool IsStaff => !string.IsNullOrEmpty(Token);
    public event Action? Changed;

    public void SetToken(string token) { Token = token; Changed?.Invoke(); }
    public void Clear() { Token = null; Changed?.Invoke(); }
}
