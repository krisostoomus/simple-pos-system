using Microsoft.Playwright;
using Reqnroll;

namespace Pos.E2E.Tests.Hooks;

[Binding]
public sealed class PlaywrightHooks(ScenarioContext context)
{
    public static string BaseUrl => Environment.GetEnvironmentVariable("POS_WEB_URL") ?? "http://127.0.0.1:8080";

    private IPlaywright? _pw;
    private IBrowser? _browser;

    [BeforeScenario]
    public async Task Setup()
    {
        _pw = await Playwright.CreateAsync();
        _browser = await _pw.Chromium.LaunchAsync(new() { Headless = true });
        var page = await _browser.NewPageAsync();
        // The app defaults to Estonian; pin scenarios to English so the English-text assertions are
        // deterministic. Only sets it when unset, so the language-switch scenario's reload keeps 'et'.
        await page.Context.AddInitScriptAsync(
            "if(!window.localStorage.getItem('pos-culture'))window.localStorage.setItem('pos-culture','en');");
        context.Set(page);
    }

    [AfterScenario]
    public async Task Teardown()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _pw?.Dispose();
    }
}
