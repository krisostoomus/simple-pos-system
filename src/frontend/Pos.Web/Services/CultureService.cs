using System.Globalization;
using Microsoft.JSInterop;

namespace Pos.Web.Services;

/// <summary>Reads/writes the active UI culture (en|et) from localStorage and applies it.</summary>
public sealed class CultureService(IJSRuntime js)
{
    public const string Key = "pos-culture";
    public static readonly string[] Supported = ["en", "et"];

    /// <summary>Default culture when nothing is persisted — Estonian, the primary on-site language.</summary>
    public const string Default = "et";

    public string Current { get; private set; } = Default;

    public async Task InitializeAsync()
    {
        var stored = await js.InvokeAsync<string?>("localStorage.getItem", Key);
        Current = Supported.Contains(stored) ? stored! : Default;
        Apply(Current);
    }

    public async Task SetAsync(string culture)
    {
        if (!Supported.Contains(culture)) return;
        await js.InvokeVoidAsync("localStorage.setItem", Key, culture);
        // Reload so the framework rebuilds with the new culture and re-fetches localized names.
        await js.InvokeVoidAsync("location.reload");
    }

    private static void Apply(string culture)
    {
        var ci = new CultureInfo(culture);
        CultureInfo.DefaultThreadCurrentCulture = ci;
        CultureInfo.DefaultThreadCurrentUICulture = ci;
    }
}
