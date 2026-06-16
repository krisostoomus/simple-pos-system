# POS Frontend (Blazor WASM) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`).

**Goal:** A neat, minimal Blazor WebAssembly POS UI — product grid with click-to-add, live running total, checkout with cash entry + smallest-change display, out-of-stock graying with live SignalR updates, Estonian/English localization, and a staff-authenticated admin page for second-hand stock — consuming the REST API over HTTP, with bUnit component tests and a Reqnroll/Playwright E2E suite run against the full docker-composed stack.

**Architecture:** `Pos.Web` is a standalone Blazor WebAssembly SPA (separate from the backend, talks to it over HTTP). MudBlazor provides UI components, themed to a custom green. A typed `PosApiClient` wraps the REST calls; `CartService` holds client-side cart state; `StockHubClient` subscribes to `/hubs/stock` for live stock; `AuthState` stores the staff JWT; localization uses `IStringLocalizer` + `.resx` with the culture persisted in `localStorage`. Product *names* come localized from the API (`Accept-Language`); UI chrome comes from `.resx`. A `docker-compose.yml` runs postgres + api + web (nginx) so the E2E suite can drive the real system.

**Tech Stack:** .NET 10, Blazor WebAssembly, MudBlazor, `Microsoft.AspNetCore.SignalR.Client`, bUnit + xUnit, Reqnroll + Microsoft.Playwright, nginx, Docker Compose.

**Plan set:** Plan 3 of 4. Prereq: Plans 1–2 merged (backend on main; API serves `/api/v1`, `/hubs/stock`, JWT `staff` auth, CORS allowlists `http://localhost:8080`). Spec: `docs/superpowers/specs/2026-06-13-pos-system-design.md`. Green palette: primary `#2E7D32`, dark `#1B5E20`, accent `#66BB6A` (a generic forest green — no brand reference).

**Conventions:** Conventional Commits with scope, NO AI co-author trailer. Money is integer cents (format as € in the UI). Solution: `PosSystem.slnx`.

---

## File Structure

```
src/frontend/Pos.Web/
  Pos.Web.csproj
  Program.cs
  _Imports.razor
  App.razor
  wwwroot/index.html
  wwwroot/appsettings.json            # ApiBaseUrl
  wwwroot/css/app.css
  wwwroot/images/                     # product images keyed by ImageKey (placeholders ok)
  Models/ApiModels.cs                 # client-side DTOs matching API JSON
  Services/PosApiClient.cs
  Services/CartService.cs
  Services/AuthState.cs
  Services/CultureService.cs
  Services/StockHubClient.cs
  Resources/UiStrings.cs              # IStringLocalizer marker
  Resources/UiStrings.en.resx
  Resources/UiStrings.et.resx
  Layout/MainLayout.razor
  Components/LanguageSwitcher.razor
  Components/ProductCard.razor
  Components/CheckoutDialog.razor
  Components/StaffLoginDialog.razor
  Pages/Sale.razor                    # the POS screen ("/")
  Pages/Admin.razor                   # staff stock entry ("/admin")
tests/Pos.Web.Tests/                  # bUnit component tests
  Pos.Web.Tests.csproj
  CartServiceTests.cs
  ChangeFormatTests.cs
  ProductCardTests.cs
docker-compose.yml                    # postgres + api + web
src/frontend/Pos.Web/Dockerfile
src/frontend/Pos.Web/nginx.conf
tests/Pos.E2E.Tests/                  # Reqnroll + Playwright
  Pos.E2E.Tests.csproj
  Features/Purchase.feature
  Features/OutOfStock.feature
  Features/Localization.feature
  Steps/PosSteps.cs
  Hooks/PlaywrightHooks.cs
```

---

## Task 1: Scaffold Blazor WASM project + MudBlazor

- [ ] **Step 1:** From repo root:
```bash
dotnet new blazorwasm -n Pos.Web -o src/frontend/Pos.Web -f net10.0
dotnet sln PosSystem.slnx add src/frontend/Pos.Web
dotnet add src/frontend/Pos.Web package MudBlazor
dotnet add src/frontend/Pos.Web package Microsoft.AspNetCore.SignalR.Client
dotnet add src/frontend/Pos.Web package Microsoft.Extensions.Localization
```
- [ ] **Step 2:** Remove template sample content: delete `Pages/Counter.razor`, `Pages/Weather.razor`, `Pages/Home.razor`, `Layout/NavMenu.razor` (and its css), and the `wwwroot/sample-data/` folder if present. Keep `App.razor`, `Program.cs`, `_Imports.razor`, `Layout/MainLayout.razor`, `wwwroot/index.html`.
- [ ] **Step 3:** Enable invariant-globalization OFF and load all cultures. In `Pos.Web.csproj` `<PropertyGroup>` add:
```xml
<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>
```
- [ ] **Step 4:** `dotnet build src/frontend/Pos.Web` → succeeds.
- [ ] **Step 5:** Commit: `git add src/frontend/Pos.Web PosSystem.slnx && git commit -m "chore(web): scaffold blazor wasm project with mudblazor"`

---

## Task 2: Client models & typed API client

- [ ] **Step 1:** Create `src/frontend/Pos.Web/Models/ApiModels.cs`:
```csharp
namespace Pos.Web.Models;

public sealed record ProductModel(
    int Id, string Name, string Category, int PriceCents,
    int StockQuantity, string ImageKey, bool IsOutOfStock);

public sealed record ChangePieceModel(int DenominationCents, int Count);

public sealed record OrderLineModel(
    int ProductId, string ProductName, int UnitPriceCents, int Quantity, int LineTotalCents);

public sealed record CheckoutResultModel(
    int OrderId, int TotalCents, int CashPaidCents, int ChangeCents,
    IReadOnlyList<ChangePieceModel> Change, IReadOnlyList<OrderLineModel> Lines, DateTime CreatedAtUtc);

public sealed record CheckoutLineModel(int ProductId, int Quantity);
public sealed record CheckoutBody(IReadOnlyList<CheckoutLineModel> Lines, int CashPaidCents);
public sealed record SetStockBody(int Quantity);
public sealed record TokenBody(string Username, string Password);
public sealed record TokenResult(string AccessToken, DateTime ExpiresAtUtc);
public sealed record ReportItemModel(int ProductId, string Name, int QuantitySold, int RevenueCents);
public sealed record ReportSummaryModel(int TotalFundsCents, int OrderCount, IReadOnlyList<ReportItemModel> Items);

/// <summary>Thrown by the API client on a non-success response; carries the API errorCode.</summary>
public sealed class ApiException(int statusCode, string errorCode, string? detail)
    : Exception($"API {statusCode}: {errorCode}")
{
    public int StatusCode { get; } = statusCode;
    public string ErrorCode { get; } = errorCode;
    public string? Detail { get; } = detail;
}
```
- [ ] **Step 2:** Create `src/frontend/Pos.Web/Services/AuthState.cs`:
```csharp
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
```
- [ ] **Step 3:** Create `src/frontend/Pos.Web/Services/PosApiClient.cs`:
```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Pos.Web.Models;

namespace Pos.Web.Services;

/// <summary>Typed wrapper over the POS REST API. Sends Accept-Language for localized names and a
/// fresh Idempotency-Key per checkout, and attaches the staff bearer token when present.</summary>
public sealed class PosApiClient(HttpClient http, AuthState auth, CultureService culture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ProductModel>> GetProductsAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/products");
        req.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture.Current));
        using var resp = await http.SendAsync(req, ct);
        await EnsureSuccess(resp);
        return (await resp.Content.ReadFromJsonAsync<List<ProductModel>>(Json, ct))!;
    }

    public async Task<CheckoutResultModel> CheckoutAsync(CheckoutBody body, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/orders")
        {
            Content = JsonContent.Create(body, options: Json),
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var resp = await http.SendAsync(req, ct);
        await EnsureSuccess(resp);
        return (await resp.Content.ReadFromJsonAsync<CheckoutResultModel>(Json, ct))!;
    }

    public async Task<bool> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        using var resp = await http.PostAsJsonAsync("api/v1/auth/token", new TokenBody(username, password), Json, ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) return false;
        await EnsureSuccess(resp);
        var token = await resp.Content.ReadFromJsonAsync<TokenResult>(Json, ct);
        auth.SetToken(token!.AccessToken);
        return true;
    }

    public async Task SetStockAsync(int productId, int quantity, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, $"api/v1/products/{productId}/stock")
        {
            Content = JsonContent.Create(new SetStockBody(quantity), options: Json),
        };
        if (auth.Token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        using var resp = await http.SendAsync(req, ct);
        await EnsureSuccess(resp);
    }

    private static async Task EnsureSuccess(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return;
        string errorCode = "error";
        string? detail = null;
        try
        {
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("errorCode", out var ec)) errorCode = ec.GetString() ?? errorCode;
            if (doc.RootElement.TryGetProperty("detail", out var d)) detail = d.GetString();
        }
        catch { /* non-JSON body */ }
        throw new ApiException((int)resp.StatusCode, errorCode, detail);
    }
}
```
- [ ] **Step 4:** `dotnet build src/frontend/Pos.Web` (will fail until `CultureService` exists — that's Task 4; reorder is fine, but to keep this task self-contained, also create the minimal `CultureService` now per Task 4 Step 1, then build). Commit after Task 4.

---

## Task 3: Cart service (client-side state)

- [ ] **Step 1:** Create `src/frontend/Pos.Web/Services/CartService.cs`:
```csharp
using Pos.Web.Models;

namespace Pos.Web.Services;

/// <summary>Client-side cart. Quantity equals clicks on a product; totals are display-only and the
/// server re-computes authoritatively at checkout.</summary>
public sealed class CartService
{
    private readonly Dictionary<int, int> _quantities = new();
    public event Action? Changed;

    public IReadOnlyDictionary<int, int> Quantities => _quantities;
    public int Count => _quantities.Values.Sum();
    public bool IsEmpty => _quantities.Count == 0;

    public int QuantityOf(int productId) => _quantities.GetValueOrDefault(productId);

    public void Add(int productId)
    {
        _quantities[productId] = QuantityOf(productId) + 1;
        Changed?.Invoke();
    }

    public void Remove(int productId)
    {
        if (!_quantities.TryGetValue(productId, out var q)) return;
        if (q <= 1) _quantities.Remove(productId);
        else _quantities[productId] = q - 1;
        Changed?.Invoke();
    }

    public int TotalCents(IReadOnlyDictionary<int, int> priceByProductId)
        => _quantities.Sum(kv => priceByProductId.GetValueOrDefault(kv.Key) * kv.Value);

    public IReadOnlyList<CheckoutLineModel> ToLines()
        => _quantities.Select(kv => new CheckoutLineModel(kv.Key, kv.Value)).ToList();

    public void Reset()
    {
        _quantities.Clear();
        Changed?.Invoke();
    }
}
```
- [ ] **Step 2:** Commit with Task 4 (needs build green).

---

## Task 4: Localization (culture service, resources, switcher)

- [ ] **Step 1:** Create `src/frontend/Pos.Web/Services/CultureService.cs`:
```csharp
using System.Globalization;
using Microsoft.JSInterop;

namespace Pos.Web.Services;

/// <summary>Reads/writes the active UI culture (en|et) from localStorage and applies it.</summary>
public sealed class CultureService(IJSRuntime js)
{
    public const string Key = "pos-culture";
    public static readonly string[] Supported = ["en", "et"];

    public string Current { get; private set; } = "en";

    public async Task InitializeAsync()
    {
        var stored = await js.InvokeAsync<string?>("localStorage.getItem", Key);
        Current = Supported.Contains(stored) ? stored! : "en";
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
```
- [ ] **Step 2:** Create `src/frontend/Pos.Web/Resources/UiStrings.cs` (marker for `IStringLocalizer<UiStrings>`):
```csharp
namespace Pos.Web.Resources;

public sealed class UiStrings;
```
- [ ] **Step 3:** Create `src/frontend/Pos.Web/Resources/UiStrings.en.resx` with these name/value pairs (standard .resx XML; values shown):
```
AppTitle = Charity Bake Sale
Total = Total
Reset = Reset
Checkout = Checkout
CashReceived = Cash received (€)
Change = Change
Pay = Pay
Cancel = Cancel
OutOfStock = Out of stock
Admin = Admin
StaffLogin = Staff login
Username = Username
Password = Password
Login = Login
Logout = Logout
SetStock = Set stock
Quantity = Quantity
Save = Save
FundsRaised = Funds raised
Edible = Edible
SecondHand = Second-hand
ErrorOutOfStock = Sorry, that item is out of stock.
ErrorInsufficientPayment = Cash received is less than the total.
ErrorGeneric = Something went wrong. Please try again.
InvalidCredentials = Invalid username or password.
```
- [ ] **Step 4:** Create `src/frontend/Pos.Web/Resources/UiStrings.et.resx` with Estonian values:
```
AppTitle = Heategevuslik küpsetiste müük
Total = Kokku
Reset = Lähtesta
Checkout = Maksma
CashReceived = Saadud sularaha (€)
Change = Tagasi
Pay = Maksa
Cancel = Tühista
OutOfStock = Otsas
Admin = Haldus
StaffLogin = Töötaja sisselogimine
Username = Kasutajanimi
Password = Parool
Login = Logi sisse
Logout = Logi välja
SetStock = Määra kogus
Quantity = Kogus
Save = Salvesta
FundsRaised = Kogutud summa
Edible = Söögikraam
SecondHand = Kasutatud kaup
ErrorOutOfStock = Kahjuks on see toode otsas.
ErrorInsufficientPayment = Saadud sularaha on väiksem kui summa.
ErrorGeneric = Midagi läks valesti. Palun proovi uuesti.
InvalidCredentials = Vale kasutajanimi või parool.
```
The standard `.resx` file is XML with a `resheader` preamble and one `<data name="X"><value>Y</value></data>` per entry. Produce valid `.resx` XML for both files with the pairs above.
- [ ] **Step 5:** Create `src/frontend/Pos.Web/Components/LanguageSwitcher.razor`:
```razor
@using Pos.Web.Services
@inject CultureService Culture

<MudMenu Icon="@Icons.Material.Filled.Language" Color="Color.Inherit" Dense="true">
    <MudMenuItem OnClick="@(() => Switch("en"))">English</MudMenuItem>
    <MudMenuItem OnClick="@(() => Switch("et"))">Eesti</MudMenuItem>
</MudMenu>

@code {
    private async Task Switch(string culture) => await Culture.SetAsync(culture);
}
```
- [ ] **Step 6:** `dotnet build src/frontend/Pos.Web` → succeeds (with Tasks 2 & 3 files present). Commit: `git add src/frontend/Pos.Web && git commit -m "feat(web): add api client, cart, localization and culture switcher"`

---

## Task 5: Program.cs, theme & layout

- [ ] **Step 1:** Replace `src/frontend/Pos.Web/Program.cs`:
```csharp
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Pos.Web;
using Pos.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

builder.Services.AddMudServices();
builder.Services.AddLocalization();
builder.Services.AddSingleton<AuthState>();
builder.Services.AddScoped<CultureService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<PosApiClient>();
builder.Services.AddScoped<StockHubClient>();

var host = builder.Build();

// Apply the persisted culture before the app renders.
var culture = host.Services.GetRequiredService<CultureService>();
await culture.InitializeAsync();

await host.RunAsync();
```
- [ ] **Step 2:** Set `src/frontend/Pos.Web/wwwroot/appsettings.json`:
```json
{ "ApiBaseUrl": "http://localhost:8081/" }
```
(The compose file maps the API to host port 8081; for `dotnet run` against a local API, override as needed.)
- [ ] **Step 3:** Update `wwwroot/index.html`: inside `<head>` add MudBlazor assets, and replace the loading markup. Add before `</head>`:
```html
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
```
and before `</body>` (after the framework script) add:
```html
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```
- [ ] **Step 4:** Replace `src/frontend/Pos.Web/Layout/MainLayout.razor`:
```razor
@inherits LayoutComponentBase
@using Pos.Web.Components
@using Pos.Web.Resources
@using Microsoft.Extensions.Localization
@inject IStringLocalizer<UiStrings> L

<MudThemeProvider Theme="@_theme" />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="2" Color="Color.Primary">
        <MudText Typo="Typo.h6">@L["AppTitle"]</MudText>
        <MudSpacer />
        <LanguageSwitcher />
        <MudButton Href="/admin" Color="Color.Inherit" StartIcon="@Icons.Material.Filled.Inventory">@L["Admin"]</MudButton>
    </MudAppBar>
    <MudMainContent>
        <MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
            @Body
        </MudContainer>
    </MudMainContent>
</MudLayout>

@code {
    private readonly MudTheme _theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2E7D32",
            Secondary = "#66BB6A",
            AppbarBackground = "#1B5E20",
        }
    };
}
```
- [ ] **Step 5:** Ensure `_Imports.razor` includes MudBlazor and project namespaces. Append:
```razor
@using MudBlazor
@using Pos.Web
@using Pos.Web.Components
@using Pos.Web.Models
@using Pos.Web.Services
@using Pos.Web.Resources
@using Microsoft.Extensions.Localization
```
- [ ] **Step 6:** `dotnet build src/frontend/Pos.Web` → succeeds. Commit: `git add src/frontend/Pos.Web && git commit -m "feat(web): configure program, mudblazor theme and layout"`

---

## Task 6: SignalR stock client

- [ ] **Step 1:** Create `src/frontend/Pos.Web/Services/StockHubClient.cs`:
```csharp
using Microsoft.AspNetCore.SignalR.Client;

namespace Pos.Web.Services;

/// <summary>Subscribes to the API's /hubs/stock and raises an event when a product's stock changes.</summary>
public sealed class StockHubClient(HttpClient http) : IAsyncDisposable
{
    private HubConnection? _connection;

    /// <summary>Fired with (productId, newQuantity) on every broadcast.</summary>
    public event Action<int, int>? StockChanged;

    public async Task StartAsync()
    {
        if (_connection is not null) return;
        var hubUrl = new Uri(http.BaseAddress!, "hubs/stock");
        _connection = new HubConnectionBuilder().WithUrl(hubUrl).WithAutomaticReconnect().Build();
        _connection.On<StockChangedDto>("StockChanged", dto => StockChanged?.Invoke(dto.ProductId, dto.NewQuantity));
        await _connection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }

    private sealed record StockChangedDto(int ProductId, int NewQuantity);
}
```
- [ ] **Step 2:** `dotnet build src/frontend/Pos.Web` → succeeds. Commit: `git add src/frontend/Pos.Web && git commit -m "feat(web): add signalr stock hub client"`

---

## Task 7: ProductCard component

- [ ] **Step 1:** Add product images. Create placeholder files under `src/frontend/Pos.Web/wwwroot/images/` named `brownie.png, muffin.png, cakepop.png, appletart.png, water.png, shirt.png, pants.png, jacket.png, toy.png` plus `placeholder.png`. (Any small PNGs; they are visual placeholders. If generating images is out of scope, create 1×1 PNGs so the `<img>` resolves; the UI uses a colored card regardless.)
- [ ] **Step 2:** Create `src/frontend/Pos.Web/Components/ProductCard.razor`:
```razor
@inject IStringLocalizer<UiStrings> L

<MudCard Class="@CardClass" Style="cursor:pointer; user-select:none;" @onclick="OnAdd">
    <MudCardMedia Image="@ImageUrl" Height="120" />
    <MudCardContent>
        <MudText Typo="Typo.subtitle1">@Product.Name</MudText>
        <MudText Typo="Typo.body2">@FormatEuro(Product.PriceCents)</MudText>
        @if (Product.IsOutOfStock)
        {
            <MudChip T="string" Color="Color.Default" Size="Size.Small">@L["OutOfStock"]</MudChip>
        }
        else
        {
            <MudText Typo="Typo.caption">@Product.StockQuantity left</MudText>
        }
        @if (Quantity > 0)
        {
            <MudBadge Content="Quantity" Color="Color.Primary" Overlap="true" Class="ml-2" />
        }
    </MudCardContent>
</MudCard>

@code {
    [Parameter, EditorRequired] public ProductModel Product { get; set; } = default!;
    [Parameter] public int Quantity { get; set; }
    [Parameter] public EventCallback<int> OnAddToCart { get; set; }

    private string CardClass => Product.IsOutOfStock ? "ma-2 mud-elevation-1 pos-disabled" : "ma-2 mud-elevation-2";
    private string ImageUrl => $"images/{Product.ImageKey}.png";

    private async Task OnAdd()
    {
        if (Product.IsOutOfStock) return;
        await OnAddToCart.InvokeAsync(Product.Id);
    }

    public static string FormatEuro(int cents) => (cents / 100m).ToString("C", System.Globalization.CultureInfo.CurrentCulture);
}
```
- [ ] **Step 3:** Add to `wwwroot/css/app.css`:
```css
.pos-disabled { filter: grayscale(100%); opacity: 0.5; pointer-events: none; }
```
- [ ] **Step 4:** `dotnet build src/frontend/Pos.Web` → succeeds. Commit: `git add src/frontend/Pos.Web && git commit -m "feat(web): add product card with out-of-stock graying"`

---

## Task 8: Checkout dialog

- [ ] **Step 1:** Create `src/frontend/Pos.Web/Components/CheckoutDialog.razor`:
```razor
@inject IStringLocalizer<UiStrings> L

<MudDialog>
    <DialogContent>
        <MudText Typo="Typo.h6">@L["Total"]: @ProductCard.FormatEuro(TotalCents)</MudText>
        <MudNumericField @bind-Value="_cashEuro" Label="@L[\"CashReceived\"]" Min="0" Step="0.05M" Format="F2" Immediate="true" />
        @if (_result is not null)
        {
            <MudAlert Severity="Severity.Success" Class="mt-3">
                @L["Change"]: @ProductCard.FormatEuro(_result.ChangeCents)
                <MudText Typo="Typo.caption">@ChangeBreakdown(_result)</MudText>
            </MudAlert>
        }
        @if (_error is not null)
        {
            <MudAlert Severity="Severity.Error" Class="mt-3">@_error</MudAlert>
        }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">@L["Cancel"]</MudButton>
        @if (_result is null)
        {
            <MudButton Color="Color.Primary" Variant="Variant.Filled" OnClick="Pay" Disabled="_busy">@L["Pay"]</MudButton>
        }
        else
        {
            <MudButton Color="Color.Primary" Variant="Variant.Filled" OnClick="Done">OK</MudButton>
        }
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public int TotalCents { get; set; }
    [Parameter] public CheckoutBody Body { get; set; } = default!;
    [Inject] private PosApiClient Api { get; set; } = default!;

    private decimal _cashEuro;
    private bool _busy;
    private string? _error;
    private CheckoutResultModel? _result;

    private async Task Pay()
    {
        _busy = true; _error = null;
        var body = Body with { CashPaidCents = (int)Math.Round(_cashEuro * 100) };
        try
        {
            _result = await Api.CheckoutAsync(body);
        }
        catch (ApiException ex)
        {
            _error = ex.ErrorCode switch
            {
                "out_of_stock" => L["ErrorOutOfStock"],
                "insufficient_payment" => L["ErrorInsufficientPayment"],
                _ => L["ErrorGeneric"],
            };
        }
        finally { _busy = false; }
    }

    private void Cancel() => Dialog.Cancel();
    private void Done() => Dialog.Close(DialogResult.Ok(true));

    private static string ChangeBreakdown(CheckoutResultModel r)
        => string.Join(", ", r.Change.Select(c => $"{c.Count}× {ProductCard.FormatEuro(c.DenominationCents)}"));
}
```
- [ ] **Step 2:** `dotnet build src/frontend/Pos.Web` → succeeds. Commit: `git add src/frontend/Pos.Web && git commit -m "feat(web): add checkout dialog with change breakdown"`

---

## Task 9: Sale page (the POS screen)

- [ ] **Step 1:** Create `src/frontend/Pos.Web/Pages/Sale.razor`:
```razor
@page "/"
@implements IDisposable
@inject PosApiClient Api
@inject CartService Cart
@inject StockHubClient Hub
@inject IDialogService Dialogs
@inject IStringLocalizer<UiStrings> L

<MudText Typo="Typo.h4" Class="mb-4">@L["AppTitle"]</MudText>

@if (_products is null)
{
    <MudProgressCircular Indeterminate="true" />
}
else
{
    <MudGrid>
        @foreach (var p in _products)
        {
            <MudItem xs="6" sm="4" md="3">
                <ProductCard Product="p" Quantity="Cart.QuantityOf(p.Id)" OnAddToCart="Add" />
            </MudItem>
        }
    </MudGrid>

    <MudPaper Class="pa-4 mt-4 d-flex align-center" Elevation="3" Style="position:sticky; bottom:0;">
        <MudText Typo="Typo.h5">@L["Total"]: @ProductCard.FormatEuro(TotalCents)</MudText>
        <MudSpacer />
        <MudButton OnClick="Reset" Disabled="Cart.IsEmpty" StartIcon="@Icons.Material.Filled.Clear" Class="mr-2">@L["Reset"]</MudButton>
        <MudButton Color="Color.Primary" Variant="Variant.Filled" OnClick="OpenCheckout" Disabled="Cart.IsEmpty"
                   StartIcon="@Icons.Material.Filled.ShoppingCartCheckout">@L["Checkout"]</MudButton>
    </MudPaper>
}

@code {
    private IReadOnlyList<ProductModel>? _products;
    private Dictionary<int, int> _priceById = new();

    private int TotalCents => Cart.TotalCents(_priceById);

    protected override async Task OnInitializedAsync()
    {
        Cart.Changed += StateHasChanged;
        Hub.StockChanged += OnStockChanged;
        await Hub.StartAsync();
        await Load();
    }

    private async Task Load()
    {
        _products = await Api.GetProductsAsync();
        _priceById = _products.ToDictionary(p => p.Id, p => p.PriceCents);
    }

    private void Add(int productId) => Cart.Add(productId);
    private void Reset() => Cart.Reset();

    private void OnStockChanged(int productId, int newQuantity)
    {
        if (_products is null) return;
        _products = _products
            .Select(p => p.Id == productId ? p with { StockQuantity = newQuantity, IsOutOfStock = newQuantity <= 0 } : p)
            .ToList();
        InvokeAsync(StateHasChanged);
    }

    private async Task OpenCheckout()
    {
        var body = new CheckoutBody(Cart.ToLines(), CashPaidCents: 0);
        var parameters = new DialogParameters { ["TotalCents"] = TotalCents, ["Body"] = body };
        var dialog = await Dialogs.ShowAsync<CheckoutDialog>(L["Checkout"], parameters);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
        {
            Cart.Reset();
            await Load(); // refresh stock after a completed sale
        }
    }

    public void Dispose()
    {
        Cart.Changed -= StateHasChanged;
        Hub.StockChanged -= OnStockChanged;
    }
}
```
- [ ] **Step 2:** `dotnet build src/frontend/Pos.Web` → succeeds. Commit: `git add src/frontend/Pos.Web && git commit -m "feat(web): add sale page with live stock and checkout"`

---

## Task 10: Staff login dialog & Admin page

- [ ] **Step 1:** Create `src/frontend/Pos.Web/Components/StaffLoginDialog.razor`:
```razor
@inject IStringLocalizer<UiStrings> L
@inject PosApiClient Api

<MudDialog>
    <DialogContent>
        <MudTextField @bind-Value="_username" Label="@L[\"Username\"]" />
        <MudTextField @bind-Value="_password" Label="@L[\"Password\"]" InputType="InputType.Password" />
        @if (_error is not null) { <MudAlert Severity="Severity.Error" Class="mt-2">@_error</MudAlert> }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => Dialog.Cancel())">@L["Cancel"]</MudButton>
        <MudButton Color="Color.Primary" Variant="Variant.Filled" OnClick="Login" Disabled="_busy">@L["Login"]</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    private string _username = "";
    private string _password = "";
    private string? _error;
    private bool _busy;

    private async Task Login()
    {
        _busy = true; _error = null;
        try
        {
            if (await Api.LoginAsync(_username, _password)) Dialog.Close(DialogResult.Ok(true));
            else _error = L["InvalidCredentials"];
        }
        catch { _error = L["ErrorGeneric"]; }
        finally { _busy = false; }
    }
}
```
- [ ] **Step 2:** Create `src/frontend/Pos.Web/Pages/Admin.razor`:
```razor
@page "/admin"
@inject PosApiClient Api
@inject AuthState Auth
@inject IDialogService Dialogs
@inject IStringLocalizer<UiStrings> L

<MudText Typo="Typo.h4" Class="mb-4">@L["Admin"]</MudText>

@if (!Auth.IsStaff)
{
    <MudButton Color="Color.Primary" Variant="Variant.Filled" OnClick="ShowLogin">@L["StaffLogin"]</MudButton>
}
else if (_products is not null)
{
    <MudTable Items="_products.Where(p => p.Category == \"SecondHand\")" Hover="true">
        <HeaderContent>
            <MudTh>@L["SecondHand"]</MudTh><MudTh>@L["Quantity"]</MudTh><MudTh></MudTh>
        </HeaderContent>
        <RowTemplate>
            <MudTd>@context.Name</MudTd>
            <MudTd>
                <MudNumericField T="int" Value="@_edits[context.Id]" Min="0"
                                 ValueChanged="@(v => _edits[context.Id] = v)" />
            </MudTd>
            <MudTd>
                <MudButton Size="Size.Small" Color="Color.Primary" OnClick="@(() => Save(context.Id))">@L["Save"]</MudButton>
            </MudTd>
        </RowTemplate>
    </MudTable>
}

@code {
    private IReadOnlyList<ProductModel>? _products;
    private readonly Dictionary<int, int> _edits = new();

    protected override async Task OnInitializedAsync()
    {
        Auth.Changed += async () => await InvokeAsync(async () => { await Load(); StateHasChanged(); });
        if (Auth.IsStaff) await Load();
    }

    private async Task Load()
    {
        _products = await Api.GetProductsAsync();
        foreach (var p in _products) _edits[p.Id] = p.StockQuantity;
    }

    private async Task ShowLogin()
    {
        var dialog = await Dialogs.ShowAsync<StaffLoginDialog>(L["StaffLogin"]);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled) await Load();
    }

    private async Task Save(int productId)
    {
        await Api.SetStockAsync(productId, _edits[productId]);
        await Load();
    }
}
```
- [ ] **Step 3:** `dotnet build src/frontend/Pos.Web` → succeeds. Commit: `git add src/frontend/Pos.Web && git commit -m "feat(web): add staff login and admin stock page"`

---

## Task 11: bUnit component tests

- [ ] **Step 1:** Scaffold:
```bash
dotnet new xunit -n Pos.Web.Tests -o tests/Pos.Web.Tests -f net10.0
rm tests/Pos.Web.Tests/UnitTest1.cs
dotnet sln PosSystem.slnx add tests/Pos.Web.Tests
dotnet add tests/Pos.Web.Tests reference src/frontend/Pos.Web
dotnet add tests/Pos.Web.Tests package bunit
```
- [ ] **Step 2:** Create `tests/Pos.Web.Tests/CartServiceTests.cs`:
```csharp
using Pos.Web.Services;

namespace Pos.Web.Tests;

public class CartServiceTests
{
    [Fact]
    public void Add_IncrementsQuantity_AndTotalUsesPrices()
    {
        var cart = new CartService();
        cart.Add(1); cart.Add(1); cart.Add(2);

        Assert.Equal(2, cart.QuantityOf(1));
        Assert.Equal(3, cart.Count);
        var total = cart.TotalCents(new Dictionary<int, int> { [1] = 65, [2] = 100 });
        Assert.Equal(2 * 65 + 100, total);
    }

    [Fact]
    public void Remove_DecrementsAndDropsAtZero()
    {
        var cart = new CartService();
        cart.Add(1);
        cart.Remove(1);
        Assert.Equal(0, cart.QuantityOf(1));
        Assert.True(cart.IsEmpty);
    }

    [Fact]
    public void Reset_ClearsCart()
    {
        var cart = new CartService();
        cart.Add(1); cart.Add(2);
        cart.Reset();
        Assert.True(cart.IsEmpty);
    }

    [Fact]
    public void ToLines_GroupsByProduct()
    {
        var cart = new CartService();
        cart.Add(1); cart.Add(1);
        var lines = cart.ToLines();
        var line = Assert.Single(lines);
        Assert.Equal(1, line.ProductId);
        Assert.Equal(2, line.Quantity);
    }
}
```
- [ ] **Step 3:** Create `tests/Pos.Web.Tests/ChangeFormatTests.cs`:
```csharp
using System.Globalization;
using Pos.Web.Components;

namespace Pos.Web.Tests;

public class ChangeFormatTests
{
    [Fact]
    public void FormatEuro_FormatsCentsAsCurrency()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-IE"); // euro, dot decimal
            Assert.Equal("€1.30", ProductCard.FormatEuro(130));
            Assert.Equal("€0.00", ProductCard.FormatEuro(0));
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }
}
```
- [ ] **Step 4:** Create `tests/Pos.Web.Tests/ProductCardTests.cs`:
```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Pos.Web.Components;
using Pos.Web.Models;
using Pos.Web.Resources;

namespace Pos.Web.Tests;

public class ProductCardTests : TestContext
{
    public ProductCardTests()
    {
        Services.AddLocalization();
        Services.AddMudServices();
    }

    [Fact]
    public void OutOfStockProduct_RendersDisabledAndDoesNotRaiseAdd()
    {
        var raised = 0;
        var product = new ProductModel(1, "Brownie", "Edible", 65, 0, "brownie", IsOutOfStock: true);
        var cut = RenderComponent<ProductCard>(p => p
            .Add(c => c.Product, product)
            .Add(c => c.OnAddToCart, _ => raised++));

        Assert.Contains("pos-disabled", cut.Markup);
        cut.Find(".mud-card").Click();
        Assert.Equal(0, raised); // disabled card swallows the click
    }

    [Fact]
    public void InStockProduct_RaisesAddOnClick()
    {
        var raised = 0;
        var product = new ProductModel(1, "Brownie", "Edible", 65, 5, "brownie", IsOutOfStock: false);
        var cut = RenderComponent<ProductCard>(p => p
            .Add(c => c.Product, product)
            .Add(c => c.OnAddToCart, _ => raised++));

        cut.Find(".mud-card").Click();
        Assert.Equal(1, raised);
    }
}
```
NOTE: `AddMudServices` requires `using MudBlazor.Services;`. If MudBlazor components need JS interop in bUnit, add `JSInterop.Mode = JSRuntimeMode.Loose;` in the constructor. Adjust selectors if MudBlazor's rendered class differs; the intent is: disabled card has `pos-disabled` and does not raise the callback; in-stock card does.
- [ ] **Step 5:** Run `dotnet test tests/Pos.Web.Tests` → green. Commit: `git add tests/Pos.Web.Tests PosSystem.slnx && git commit -m "test(web): add bunit tests for cart, formatting and product card"`

---

## Task 12: Web Dockerfile + nginx + docker-compose

- [ ] **Step 1:** Create `src/frontend/Pos.Web/nginx.conf`:
```nginx
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;
    location / {
        try_files $uri $uri/ /index.html;
    }
    # Correct MIME for Blazor wasm/dll assets
    location /_framework/ {
        types { application/wasm wasm; application/octet-stream dll; }
        try_files $uri =404;
    }
}
```
- [ ] **Step 2:** Create `src/frontend/Pos.Web/Dockerfile` (build context = repo root):
```dockerfile
# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PosSystem.slnx ./
COPY src/frontend/Pos.Web/Pos.Web.csproj src/frontend/Pos.Web/
RUN dotnet restore src/frontend/Pos.Web/Pos.Web.csproj
COPY src/frontend/ ./src/frontend/
RUN dotnet publish src/frontend/Pos.Web/Pos.Web.csproj -c Release -o /app

FROM nginx:alpine AS runtime
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY src/frontend/Pos.Web/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```
- [ ] **Step 3:** Create `src/frontend/Pos.Web/.dockerignore`:
```
bin/
obj/
```
- [ ] **Step 4:** The published WASM bakes `wwwroot/appsettings.json` with `ApiBaseUrl`. For the composed stack the browser reaches the API on the host at `http://localhost:8081/`. Confirm `wwwroot/appsettings.json` is `{ "ApiBaseUrl": "http://localhost:8081/" }` (set in Task 5). (The browser, not the web container, calls the API, so it must be the host-published API URL.)
- [ ] **Step 5:** Create `docker-compose.yml` at the repo root:
```yaml
services:
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: pos
      POSTGRES_USER: pos
      POSTGRES_PASSWORD: pos
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U pos"]
      interval: 5s
      timeout: 5s
      retries: 10
    volumes:
      - pos-db:/var/lib/postgresql/data

  api:
    build:
      context: .
      dockerfile: src/backend/Pos.Api/Dockerfile
    environment:
      ConnectionStrings__Postgres: "Host=db;Port=5432;Database=pos;Username=pos;Password=pos"
      Cors__Origins__0: "http://localhost:8080"
      Jwt__SigningKey: "compose-dev-signing-key-change-me-32bytes!!"
      StaffCredential__Password: "staff-password"
    ports:
      - "8081:8080"
    depends_on:
      db:
        condition: service_healthy

  web:
    build:
      context: .
      dockerfile: src/frontend/Pos.Web/Dockerfile
    ports:
      - "8080:80"
    depends_on:
      - api

volumes:
  pos-db:
```
- [ ] **Step 6:** Build the images and bring the stack up to verify:
```bash
docker compose build
docker compose up -d
```
Wait for health, then check: `curl -s http://localhost:8081/health` (Healthy) and `curl -s http://localhost:8081/api/v1/products` (9 products) and open `http://localhost:8080` (the SPA). Then `docker compose down`.
- [ ] **Step 7:** Commit: `git add src/frontend/Pos.Web/Dockerfile src/frontend/Pos.Web/nginx.conf src/frontend/Pos.Web/.dockerignore docker-compose.yml && git commit -m "build(web): add web dockerfile, nginx config and docker-compose stack"`

---

## Task 13: Reqnroll + Playwright E2E

These run against the composed stack (web on :8080, api on :8081).

- [ ] **Step 1:** Scaffold:
```bash
dotnet new xunit -n Pos.E2E.Tests -o tests/Pos.E2E.Tests -f net10.0
rm tests/Pos.E2E.Tests/UnitTest1.cs
dotnet sln PosSystem.slnx add tests/Pos.E2E.Tests
dotnet add tests/Pos.E2E.Tests package Reqnroll.xUnit
dotnet add tests/Pos.E2E.Tests package Microsoft.Playwright
```
- [ ] **Step 2:** Create `tests/Pos.E2E.Tests/Features/Purchase.feature`:
```gherkin
Feature: Purchase flow
  As a seller I can add items and check out, receiving correct change.

  Scenario: Buy two brownies and pay with a five euro note
    Given the POS app is open
    When I click the "Brownie" product 2 times
    Then the running total shows "1.30"
    When I checkout with cash "5.00"
    Then the change shown is "3.70"
```
- [ ] **Step 3:** Create `tests/Pos.E2E.Tests/Features/OutOfStock.feature`:
```gherkin
Feature: Out of stock
  Second-hand items start at zero stock and are grayed out.

  Scenario: A zero-stock item is disabled
    Given the POS app is open
    Then the "Jacket" product is grayed out
```
- [ ] **Step 4:** Create `tests/Pos.E2E.Tests/Features/Localization.feature`:
```gherkin
Feature: Localization
  The UI and product names can switch language.

  Scenario: Switch to Estonian
    Given the POS app is open
    When I switch the language to Estonian
    Then the checkout button reads "Maksma"
```
- [ ] **Step 5:** Create `tests/Pos.E2E.Tests/Hooks/PlaywrightHooks.cs` (manages the browser; base URL from env `POS_WEB_URL`, default `http://localhost:8080`):
```csharp
using Microsoft.Playwright;
using Reqnroll;

namespace Pos.E2E.Tests.Hooks;

[Binding]
public sealed class PlaywrightHooks(ScenarioContext context)
{
    public static string BaseUrl => Environment.GetEnvironmentVariable("POS_WEB_URL") ?? "http://localhost:8080";

    private IPlaywright? _pw;
    private IBrowser? _browser;

    [BeforeScenario]
    public async Task Setup()
    {
        _pw = await Playwright.CreateAsync();
        _browser = await _pw.Chromium.LaunchAsync(new() { Headless = true });
        var page = await _browser.NewPageAsync();
        context.Set(page);
    }

    [AfterScenario]
    public async Task Teardown()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _pw?.Dispose();
    }
}
```
- [ ] **Step 6:** Create `tests/Pos.E2E.Tests/Steps/PosSteps.cs`:
```csharp
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
        await Page.GotoAsync(PlaywrightHooks.BaseUrl);
        await Page.GetByText("Charity Bake Sale").First.WaitForAsync(new() { Timeout = 30_000 });
    }

    [When(@"I click the ""(.*)"" product (\d+) times")]
    public async Task WhenIClickProduct(string name, int times)
    {
        var card = Page.Locator(".mud-card", new() { HasText = name }).First;
        for (var i = 0; i < times; i++) await card.ClickAsync();
    }

    [Then(@"the running total shows ""(.*)""")]
    public async Task ThenTotalShows(string amount)
        => await Page.GetByText($"Total: €{amount}").WaitForAsync(new() { Timeout = 10_000 });

    [When(@"I checkout with cash ""(.*)""")]
    public async Task WhenCheckout(string cash)
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Checkout" }).ClickAsync();
        await Page.GetByLabel("Cash received").FillAsync(cash);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Pay" }).ClickAsync();
    }

    [Then(@"the change shown is ""(.*)""")]
    public async Task ThenChangeShown(string amount)
        => await Page.GetByText($"Change: €{amount}").WaitForAsync(new() { Timeout = 10_000 });

    [Then(@"the ""(.*)"" product is grayed out")]
    public async Task ThenGrayedOut(string name)
    {
        var card = Page.Locator(".pos-disabled", new() { HasText = name }).First;
        await card.WaitForAsync(new() { Timeout = 10_000 });
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
```
NOTE: selectors may need tuning to the actual rendered MudBlazor markup — adjust them while running so each scenario passes. The euro symbol/format depends on culture; the default culture is `en`, which renders `€1.30`. Keep currency assertions consistent with the active culture.
- [ ] **Step 7:** Build, install Playwright browsers, ensure the stack is up, then run:
```bash
dotnet build tests/Pos.E2E.Tests
pwsh tests/Pos.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
docker compose up -d --wait
dotnet test tests/Pos.E2E.Tests
docker compose down
```
Iterate on selectors until the three scenarios pass. If the environment cannot run the browser or the composed stack, ensure the project builds and the features/steps are correct, document the run command, and report which scenarios were verified.
- [ ] **Step 8:** Commit: `git add tests/Pos.E2E.Tests PosSystem.slnx && git commit -m "test(e2e): add reqnroll + playwright scenarios for purchase, out-of-stock and localization"`

---

## Task 14: Full verification

- [ ] **Step 1:** `dotnet build PosSystem.slnx` → succeeds (warnings acceptable; aim for zero).
- [ ] **Step 2:** Run the non-E2E suite (fast, no stack): `dotnet test PosSystem.slnx --filter "FullyQualifiedName!~Pos.E2E"` → domain + application + api + web bUnit all green.
- [ ] **Step 3:** Confirm the stack runs: `docker compose up -d --wait`, open `http://localhost:8080`, buy an item, check out, observe change; open a second browser tab and confirm a sale grays the relevant item live; switch language. `docker compose down`.
- [ ] **Step 4:** Commit any final fixes: `git commit -am "fix(web): <description>"` (only if needed).

---

## Definition of Done (Plan 3)

- Blazor WASM app: product grid, click-to-add, sticky running total, reset + checkout, change breakdown, out-of-stock graying with live SignalR updates, EN/ET localization with a language switcher, staff login + admin second-hand stock entry.
- bUnit tests green (cart, formatting, product card).
- `docker compose up` runs db + api + web; the SPA works end-to-end against the API.
- Reqnroll/Playwright E2E scenarios (purchase, out-of-stock, localization) run against the composed stack (or are correct + documented if the environment can't run a browser).

**Next:** Plan 4 (docs) — C4 + Mermaid diagrams under `docs/architecture/`, root README (lean, with quick start + links), per-component READMEs (`src/backend`, `src/frontend`), and `DEPLOY.md`.
