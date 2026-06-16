# Charity Bake Sale POS

A point-of-sale system for a one-day charity bake sale and second-hand outlet. Multiple salespeople
ring up sales concurrently on tablets or phones: they tap product images to build a cart, see a live
running total, and check out by entering the cash received — the system calculates the smallest
change to give back and records the sale so the charity can see the funds raised.

Built with **.NET 10**, **Blazor WebAssembly + MudBlazor**, **ASP.NET Core minimal API**,
**PostgreSQL (EF Core 10)**, **SignalR**, and **Docker Compose**.

## Features

- **Touch-friendly sale screen** — product grid, tap to add (quantity = taps), sticky running total, reset & checkout.
- **Smallest-change calculation** — greedy over euro denominations; full piece breakdown; money handled as integer cents (no floating point).
- **Concurrency-safe stock** — checkout decrements stock in one transaction with optimistic concurrency (Postgres `xmin`); two sellers racing for the last item → exactly one succeeds.
- **Live stock across devices** — SignalR broadcasts stock changes; sold-out items gray out on every tablet instantly.
- **Idempotent checkout** — an `Idempotency-Key` makes double-submits (flaky networks) safe, including the concurrent-duplicate case.
- **Localization (English / Estonian)** — UI chrome from `.resx`; product names from the API via `Accept-Language` with fallback to the canonical name.
- **Staff-only admin** — JWT-protected: set second-hand stock on the day, view the funds-raised summary.
- **Config-file seeding (bonus)** — catalog, prices, quantities and translations seeded from `seed.json` on startup.
- **Documented REST API** — versioned (`/api/v1`), OpenAPI + Swagger UI, RFC 9457 `ProblemDetails` with machine-readable error codes.

## Architecture

Separated frontend and backend over HTTP, three containers (web · api · db). The API uses
lightweight Clean Architecture (Domain → Application → Infrastructure → Api).

See **[docs/architecture/README.md](docs/architecture/README.md)** for the C4 diagrams, the checkout
sequence, and the ER model.

```
src/backend/   Pos.Domain · Pos.Application · Pos.Infrastructure · Pos.Api   (see src/backend/README.md)
src/frontend/  Pos.Web  (Blazor WASM + MudBlazor)                            (see src/frontend/README.md)
tests/         Domain · Application · Api (Testcontainers) · Web (bUnit) · E2E (Reqnroll + Playwright)
docs/          architecture diagrams, design spec & plans
```

## Quick start

Requires **Docker** (with Compose). From the repo root:

```bash
docker compose up --build
```

Then open:

| What | URL |
|---|---|
| POS app | http://localhost:8080 |
| Swagger UI | http://localhost:8081/swagger |
| Health | http://localhost:8081/health |

The API applies database migrations and seeds the catalog automatically on startup.

> **Note:** browse to the app using the same host spelling the API allows. CORS allowlists both
> `http://localhost:8080` and `http://127.0.0.1:8080`; if you change ports/origins, update the API's
> `Cors:Origins` accordingly. Full details in **[DEPLOY.md](DEPLOY.md)**.

### Staff login (demo)

The admin page (`/admin`) and the funds-raised report require a staff token. The compose dev
credentials are:

- **Username:** `staff`
- **Password:** `staff-password`

(Dev-only values — see [DEPLOY.md](DEPLOY.md) for configuring real secrets.)

## Running the tests

```bash
# Everything except the browser E2E (no running stack needed; API tests use Testcontainers + Docker)
dotnet test PosSystem.slnx --filter "FullyQualifiedName!~Pos.E2E"

# End-to-end (Reqnroll + Playwright) — needs the stack up and a Playwright browser
docker compose up -d --wait
pwsh tests/Pos.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/Pos.E2E.Tests
docker compose down
```

Coverage: Domain (27) · Application (21) · API integration on real Postgres (15) · Web bUnit (7) ·
E2E scenarios (3: purchase + change, out-of-stock graying, language switch).

## Assumptions & trade-offs

- **Anonymous selling, authenticated admin.** The selling flow needs no login (matches the tablet
  UX); only stock changes and reports are protected. Full identity (per-seller accounts, OIDC) is out of scope.
- **Fake payment service.** `IPaymentService` validates cash and returns change — the seam where a
  real card/PSP would plug in.
- **Optimistic concurrency over locking.** Chosen for a low-contention bake sale; pessimistic locks
  or stock reservations would be the next step under heavy contention.
- **Unlimited cash drawer.** Greedy change is optimal for the full euro denomination set; finite
  drawer tracking is out of scope.
- **Migrate + seed on startup** assumes a single API instance (true here).

## What I'd improve for production

Full identity over the existing JWT seam (per-seller accounts, OIDC/SSO, refresh tokens); a real
payment provider; finite cash-drawer tracking with denomination-aware change; pessimistic locking or
stock reservations under heavy contention; a CI/CD pipeline; multi-event support.

## Documentation

- [Architecture & diagrams](docs/architecture/README.md)
- [Deployment guide](DEPLOY.md)
- [Backend README](src/backend/README.md) · [Frontend README](src/frontend/README.md)
- [Design spec](docs/superpowers/specs/2026-06-13-pos-system-design.md) and [implementation plans](docs/superpowers/plans/)
