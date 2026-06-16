namespace Pos.Application;

/// <summary>Culture utilities shared across application services.</summary>
public static class CultureHelper
{
    /// <summary>Reduces a culture (e.g. an <c>Accept-Language</c> value like "et-EE") to its neutral,
    /// lower-cased code ("et"), or <c>null</c> when none is supplied.</summary>
    public static string? ToNeutral(string? culture)
        => string.IsNullOrWhiteSpace(culture) ? null : culture.Split('-')[0].ToLowerInvariant();
}
