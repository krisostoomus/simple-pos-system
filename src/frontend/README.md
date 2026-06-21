# Frontend

`Pos.Web` — a standalone **Blazor WebAssembly** SPA (MudBlazor) that consumes the REST API over
HTTP and subscribes to the SignalR stock hub. Served as static files by nginx in production.

## Layout

| Path | Responsibility |
|---|---|
| `Pages/Sale.razor` | The POS screen (`/`): product grid, sticky total, reset/checkout, live stock. |
| `Pages/Admin.razor` | Staff page (`/admin`): login + set second-hand stock. |
| `Components/` | `ProductCard`, `CheckoutDialog`, `StaffLoginDialog`, `LanguageSwitcher`. |
| `Services/` | `PosApiClient` (typed HTTP), `CartService` (client-side cart), `StockHubClient` (SignalR), `AuthState` (JWT), `CultureService` (culture in `localStorage`). |
| `Models/ApiModels.cs` | Client-side records matching the API JSON (frontend stays decoupled from backend assemblies). |
| `Resources/UiStrings.*.resx` | UI chrome strings for English & Estonian. |

## Key decisions

- **Separated from the backend** — the SPA talks to the API only over HTTP; it does not reference any backend project. `ApiBaseUrl` is configured in `wwwroot/appsettings.json`.
- **Client-side cart**, server-authoritative totals. Quantity = taps on a product image.
- **Live stock** via SignalR `StockChanged` → out-of-stock cards gray out (CSS `pos-disabled`) in real time.
- **Localization**: UI chrome via `IStringLocalizer` + `.resx`; product names arrive already-localized from the API (`Accept-Language`) with canonical fallback. The language switcher persists the culture and reloads.
- **Checkout** mints one `Idempotency-Key` per checkout (in `CheckoutDialog`) and reuses it across the retry and any manual re-submit, so the server replays the original order rather than duplicating it; maps API `errorCode`s to localized messages, and auto-retries once on a transient `concurrency_conflict`.
- **Theme**: minimalist bakery aesthetic on a Mint Emerald (`#059669`) accent. All UI must follow the
  tokens in [DESIGN_SYSTEM.md](Pos.Web/DESIGN_SYSTEM.md) (60/30/10 color budget, Deep Slate text,
  rounded cards, high negative space).

## Run & test

```bash
# Component tests (bUnit)
dotnet test tests/Pos.Web.Tests

# Run against a local API (set wwwroot/appsettings.json ApiBaseUrl to the API URL)
dotnet run --project src/frontend/Pos.Web
```

In the composed stack the SPA is served at http://localhost:8080 and calls the API at
`http://localhost:8081`. See [../../DEPLOY.md](../../DEPLOY.md).
