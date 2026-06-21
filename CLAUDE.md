# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

A point-of-sale system for a one-day charity bake sale: salespeople tap product images on tablets to
build a cart, see a live total, and check out by entering cash received (the system computes change).
.NET 10 · Blazor WASM + MudBlazor · ASP.NET Core minimal API · PostgreSQL (EF Core 10) · SignalR · Docker.

## Commands

```bash
# Run the whole stack (web :8080, api :8081, db) — applies migrations + seeds on startup
docker compose up --build              # add -d --wait to run detached and block until healthy

# Build
dotnet build PosSystem.slnx

# Test — everything except the browser E2E (API tests use Testcontainers, so Docker must be running)
dotnet test PosSystem.slnx --filter "FullyQualifiedName!~Pos.E2E"

# A single project / test
dotnet test tests/Pos.Domain.Tests
dotnet test PosSystem.slnx --filter "FullyQualifiedName~ChangeCalculatorTests"
dotnet test PosSystem.slnx --filter "DisplayName~gives smallest change"

# End-to-end (Reqnroll + Playwright) — needs the stack up and a browser installed
docker compose up -d --wait
pwsh tests/Pos.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/Pos.E2E.Tests

# Run a project alone (API needs a Postgres at ConnectionStrings:Postgres)
dotnet run --project src/backend/Pos.Api
dotnet run --project src/frontend/Pos.Web      # set wwwroot/appsettings.json ApiBaseUrl first

# EF Core migrations (a design-time PosDbContextFactory exists, so no startup project needed)
dotnet ef migrations add <Name> --project src/backend/Pos.Infrastructure
```

Staff login (dev): `staff` / `staff-password`. Swagger at http://localhost:8081/swagger.

## Architecture

Frontend and backend are **separated over HTTP** (three containers: web · api · db). The SPA does not
reference any backend assembly — it has its own `Models/ApiModels.cs` records mirroring the API JSON.

**Backend = lightweight Clean Architecture**, dependencies pointing inward:
`Pos.Api` → `Pos.Application` → `Pos.Domain`, with `Pos.Infrastructure` implementing Application's port
interfaces and wired only at the composition root (`AddInfrastructure`). Don't add outward dependencies
(e.g. Domain must stay free of EF/ASP.NET). See `src/backend/README.md`.

These cross-cutting contracts span multiple files and are the things most likely to bite:

- **Money is integer cents end to end** — never floating point. `ChangeCalculator` (Domain) is a pure
  greedy function over euro denominations.
- **Error-code contract between backend and frontend.** The API returns RFC 9457 `ProblemDetails`
  carrying a language-neutral `errorCode` (`out_of_stock`, `insufficient_payment`, `empty_cart`,
  `invalid_quantity`, `unknown_product`, `concurrency_conflict`, `not_found`,
  `missing_idempotency_key`). The SPA maps these codes to localized messages itself (falling back to a
  generic message for codes with no user-actionable case). Adding a failure mode means adding the code
  in **both** places.
- **Concurrency + idempotency at checkout.** Stock decrements in one transaction with optimistic
  concurrency via the Postgres `xmin` column; `UnitOfWork` translates EF's `DbUpdateConcurrencyException`
  to `ConcurrencyConflictException` and a unique `IdempotencyKey` violation to
  `DuplicateIdempotencyKeyException` (idempotent replay). The key identifies the checkout *intent*: the
  client generates one GUID per checkout (in `CheckoutDialog`) and reuses it across the conflict-retry
  and any manual re-submit, so an ambiguous failure replays the original order instead of duplicating
  it. The API **rejects** a missing/non-GUID key (`400 missing_idempotency_key`) rather than fabricating
  one. The client auto-retries once on a transient `concurrency_conflict`.
- **Client-side cart, server-authoritative totals.** `CartService` (frontend) holds quantities (quantity
  = taps on a product); displayed totals are advisory and the server **recomputes authoritatively** at
  checkout. Order lines snapshot product name + unit price for historical accuracy.
- **Live stock via SignalR.** Checkout broadcasts `StockChanged` on `/hubs/stock`; `StockHubClient` →
  `Sale.razor` updates cards so sold-out items gray out on every device in real time.
- **Localization is split:** UI chrome comes from `Resources/UiStrings.*.resx` via `IStringLocalizer`;
  product names arrive **already localized** from the API (driven by `Accept-Language`, canonical-name
  fallback). The language switcher persists culture to `localStorage` and reloads.

The API runs `Database.MigrateAsync()` then seeds the catalog from
`src/backend/Pos.Infrastructure/Seeding/seed.json` if empty, on startup — this assumes a **single API
instance**. Second-hand items seed at 0 stock; staff set them via `/admin`.

## Gotchas

- **CORS/origin must match.** The browser calls the API directly; browse to the app via an origin in the
  API's `Cors:Origins` allowlist (`localhost:8080` / `127.0.0.1:8080`) and stay consistent. See `DEPLOY.md`.
- **Startup race:** the `api` container can report healthy a beat before Kestrel serves the port, so the
  first request after `up --wait` may come back empty — retry or poll `/health`.
- No central package management or `Directory.Build.props` — package versions live in each `.csproj`.

See `README.md`, `DEPLOY.md`, `src/backend/README.md`, `src/frontend/README.md`, and
`docs/architecture/README.md` for deeper detail.
