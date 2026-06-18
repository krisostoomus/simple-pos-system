using Microsoft.Playwright;
using Pos.E2E.Tests.Hooks;
using Reqnroll;

namespace Pos.E2E.Tests.Steps;

[Binding]
public sealed class PosSteps(ScenarioContext context)
{
    private IPage Page => context.Get<IPage>();

    [Given("the POS app is open")]
    public async Task GivenAppOpen()
    {
        await Page.GotoAsync(PlaywrightHooks.BaseUrl, new() { WaitUntil = WaitUntilState.Commit, Timeout = 60_000 });
        // Wait for the app bar title to appear (Blazor WASM rendered)
        await Page.GetByText("Charity Bake Sale").First.WaitForAsync(new() { Timeout = 90_000 });
        // Wait for product cards to load (API call complete)
        await Page.Locator(".pos-card").First.WaitForAsync(new() { Timeout = 30_000 });
    }

    [When(@"I click the ""(.*)"" product (\d+) times")]
    public async Task WhenIClickProduct(string name, int times)
    {
        var card = Page.Locator(".pos-card", new() { HasText = name }).First;
        for (var i = 0; i < times; i++) await card.ClickAsync();
    }

    [Then(@"the running total shows ""(.*)""")]
    public async Task ThenTotalShows(string amount)
        // Use a partial text locator to handle any currency symbol encoding variation
        => await Page.Locator($"text=/Total:.*{amount}/").WaitForAsync(new() { Timeout = 15_000 });

    [When(@"I checkout with cash ""(.*)""")]
    public async Task WhenCheckout(string cash)
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Checkout", Exact = false }).ClickAsync();
        // MudNumericField renders as type=text; wait for dialog to appear, then fill input
        await Page.Locator(".mud-dialog input").WaitForAsync(new() { Timeout = 10_000 });
        await Page.Locator(".mud-dialog input").First.FillAsync(cash);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Pay", Exact = false }).ClickAsync();
    }

    [Then(@"the change shown is ""(.*)""")]
    public async Task ThenChangeShown(string amount)
        => await Page.Locator($"text=/Change:.*{amount}/").WaitForAsync(new() { Timeout = 15_000 });

    [Then(@"the ""(.*)"" product is grayed out")]
    public async Task ThenGrayedOut(string name)
    {
        var card = Page.Locator(".pos-card--disabled", new() { HasText = name }).First;
        await card.WaitForAsync(new() { Timeout = 30_000 });
    }

    [When("I switch the language to Estonian")]
    public async Task WhenSwitchEstonian()
    {
        await Page.Locator("button:has(.mud-icon-root)").First.ClickAsync(); // language menu
        await Page.GetByText("Eesti").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    [Then(@"the checkout button reads ""(.*)""")]
    public async Task ThenCheckoutReads(string text)
        => await Page.GetByRole(AriaRole.Button, new() { Name = text }).WaitForAsync(new() { Timeout = 10_000 });
}
