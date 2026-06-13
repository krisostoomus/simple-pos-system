# Charity Bake Sale POS — Design Spec

**Date:** 2026-06-13
**Status:** Approved for planning

## 1. Purpose & context

A point-of-sale system for a one-day charity bake sale and second-hand outlet. Multiple
salespersons use tablets or phones to ring up sales concurrently. They tap product images to
build a cart, see a running total, and check out by entering the cash received; the system
calculates the smallest change to give back. The charity's goal is raising funds, so completed
sales are recorded and a simple "funds raised" summary is available.

This is also a senior-engineer work sample. The design therefore optimizes for the qualities the
exercise is really testing: clean architecture, concurrency awareness (multiple sellers), clear
API design, testability, and disciplined edge-case handling — without over-engineering a bake-sale
app.

### Catalog

Edible items (fixed price and starting quantity, seeded from config):

| Item | Price | Qty |
|---|---|---|
| Brownie | €0.65 | 48 |
| Muffin | €1.00 | 36 |
| Cake Pop | €1.35 | 24 |
| Apple tart | €1.50 | 60 |
| Water | €1.50 | 30 |

Second-hand items (fixed price; **quantity entered on the day** via an admin page, starts at 0):

| Item | Price |
|---|---|
| Shirt | €2.00 |
| Pants | €3.00 |
| Jacket | €4.00 |
| Toy | €1.00 |

## 2. Scope

**In scope:** product catalog with live stock, click-to-add cart, running total, checkout with
cash entry and smallest-change calculation, transactional stock decrement with optimistic
concurrency, live stock sync across tablets, order persistence, funds-raised summary, admin stock
entry for second-hand items, config-file seeding (bonus), Estonian/English localization, Swagger,
Docker Compose, unit + integration + Gherkin E2E tests, C4 + Mermaid docs.

**Out of scope (documented as "what I'd improve"):** authentication/login, real payment provider,
finite cash-drawer tracking, CI/CD pipeline, multi-day/multi-event support, reservations.

## 3. Architecture

Single solution, separated frontend and backend, three deployable services (web · api · db).

```
src/
  backend/
    Pos.Domain          # entities, value objects, change-making logic — pure, no external deps
    Pos.Application     # use-case services, port interfaces, DTOs, validation
    Pos.Infrastructure  # EF Core, repositories, migrations, JSON seeder, fake payment, SignalR
    Pos.Api             # minimal API endpoints, Swagger, exception->ProblemDetails, CORS, hub map
  frontend/
    Pos.Web             # Blazor WebAssembly (standalone) + MudBlazor; consumes REST + SignalR
tests/
    Pos.Domain.Tests        # xUnit unit tests
    Pos.Application.Tests   # xUnit + NSubstitute (mocked ports)
    Pos.Api.Tests           # xUnit + WebApplicationFactory + Testcontainers Postgres
    Pos.E2E.Tests           # Reqnroll (Gherkin) + Playwright against the composed stack
```

**Decisions:**

- **Lightweight Clean Architecture.** Dependency direction Api → Application → Domain;
  Infrastructure implements Application's port interfaces and is wired only at the composition
  root. Each layer is kept thin — no speculative abstractions, no repository-per-entity, no mapper
  library unless it earns its place. Interfaces exist only at real seams: persistence, payment,
  seeding, real-time notification.
- **Blazor WebAssembly (standalone)**, not Blazor Server — gives genuine frontend/backend
  separation over HTTP. The web app is a static SPA served from its own container.
- **MudBlazor** for UI — neat, minimal, themable to a custom green palette.
- **.NET 10** target. **Money is integer cents everywhere; never floating point.**
- **Localization: Estonian + English** (see §8).

> Branding note: the UI uses a generic custom green palette. No company name or brand is referenced
> anywhere in code, docs, or UI.

## 4. Data model

- **Product**: `Id, Name, Category (Edible | SecondHand), PriceCents, StockQuantity, ImageKey,
  IsActive, RowVersion`
- **Order**: `Id, CreatedAtUtc, TotalCents, CashPaidCents, ChangeCents` + lines
- **OrderLine**: `Id, OrderId, ProductId, ProductName (snapshot), UnitPriceCents (snapshot),
  Quantity, LineTotalCents`

Order lines **snapshot** product name and unit price so historical orders remain correct if a
product later changes. `RowVersion` is the optimistic-concurrency token on Product.

`ProductName` is stored as a stable catalog key; the **display name is localized client-side** via
resource files (see §8), so the database stays language-neutral.

## 5. REST API

Follows REST best practices (plural nouns, correct status codes, `ProblemDetails` for errors,
documented with Swagger/OpenAPI).

| Method | Route | Purpose | Responses |
|---|---|---|---|
| GET | `/api/products` | Catalog with price + live stock | 200 |
| GET | `/api/products/{id}` | Single product | 200 / 404 |
| PUT | `/api/products/{id}/stock` | Set second-hand quantity on the day (admin) | 200 / 404 / 400 |
| POST | `/api/orders` | Checkout — lines + cashPaidCents | 201 / 409 / 422 / 400 |
| GET | `/api/orders/{id}` | Order detail incl. change breakdown | 200 / 404 |
| GET | `/api/reports/summary` | Funds raised, items sold | 200 |
| Hub | `/hubs/stock` | SignalR `StockChanged(productId, newQuantity)` | — |

The **cart is client-side only** — there are no cart endpoints. The server always recomputes totals
authoritatively from current prices at checkout; client-side totals are display only.

Status-code semantics for checkout:
- **409 Conflict** — insufficient stock for a line, or optimistic-concurrency conflict; body names
  the offending product.
- **422 Unprocessable Entity** — cash paid is less than the total.
- **400 Bad Request** — empty cart, zero/negative quantities, unknown product.
- **201 Created** — success; body includes order id, total, and the change breakdown.

## 6. Checkout & concurrency flow

1. Seller taps product images → client cart; running total shown at the bottom (display only).
2. Checkout modal prompts for cash received.
3. `POST /api/orders` with line items + `cashPaidCents`.
4. Server, in **one DB transaction**: load products → validate stock per line (else 409, naming the
   item) → validate `cashPaid ≥ total` (else 422) → decrement stock → `SaveChanges` with
   **optimistic concurrency** via `RowVersion`. On `DbUpdateConcurrencyException`: reload and retry
   once; if still conflicting → 409.
5. Compute change with the pure **ChangeCalculator** (greedy over standard euro denominations:
   500, 200, 100, 50, 20, 10, 5, 2, 1 € and 50, 20, 10, 5, 2, 1 c), returning the full piece
   breakdown. Greedy is optimal for the euro denomination set; the drawer is assumed unlimited.
6. Persist the Order → commit → broadcast `StockChanged` for each affected product.
7. Return 201 with order id, total, and change breakdown.

All connected tablets receive `StockChanged` over SignalR and **gray out** items that reach zero —
live. **Trade-off (documented in README):** optimistic concurrency is chosen over pessimistic
row-locking for a low-contention bake sale; pessimistic locking or a reservation model would be the
next step under heavy contention.

The **payment service is a fake** behind an `IPaymentService` interface: it validates the cash
amount and returns change, standing in for where a real card/PSP integration would plug in. It is
mockable in tests.

## 7. Edge cases (handled and tested)

- Out of stock → image grayed, client blocks adding, server rejects with 409.
- Tapping beyond available stock → server is authoritative and rejects.
- Insufficient payment → 422, **no** stock decrement, transaction rolled back.
- Exact payment → zero change.
- Overpayment → smallest-piece change breakdown.
- Concurrent last-item sale by two sellers → exactly one succeeds, the other gets 409.
- Empty cart checkout → 400.
- Zero or negative quantities → validation error (400).
- Unknown product id → 400/404.
- Second-hand items start at 0 stock until entered on the day (grayed until then).
- All money handled as integer cents; no floating-point arithmetic.

## 8. Localization

- **Languages: Estonian (`et`) and English (`en`).** English is the default; a language switcher in
  the UI toggles culture, persisted to `localStorage`.
- Implemented with standard .NET localization: `IStringLocalizer` + `.resx` resource files per
  culture for all user-facing strings — product display names, buttons, checkout/change labels,
  validation and error messages.
- Numbers and currency are formatted per the active culture.
- The database stays language-neutral (stores a stable product key); display names are resolved
  client-side.
- E2E scenarios include switching language and asserting localized labels.

## 9. Seeding (bonus)

A `seed.json` configuration file holds edible items (name, price, quantity) and second-hand items
(name, price, quantity 0). On startup the seeder upserts items into the database if the catalog is
empty. File path and values are configurable, satisfying the bonus "read items, quantity and price
from a configuration file to database."

## 10. Docker

`docker compose up` brings up the whole system:

- **postgres** — database, healthchecked, named volume for persistence.
- **api** — applies EF migrations and runs the seeder on startup, healthchecked; depends on a
  healthy postgres.
- **web** — nginx serving the compiled Blazor WebAssembly bundle; depends on the api.

CORS on the api is configured for the web origin. Connection strings and the seed file path come
from environment/config. `DEPLOY.md` documents the full bring-up.

## 11. Testing strategy

- **Unit (xUnit):** Domain `ChangeCalculator` exhaustively (every cent 0–N, exact/over/zero), stock
  and checkout rules; Application services with **NSubstitute**-mocked ports (payment, repos,
  notifier).
- **Integration (xUnit + WebApplicationFactory + Testcontainers Postgres):** all endpoints against a
  real Postgres, seeding, and a **parallel-checkout concurrency test** asserting exactly one 409 on
  the last item.
- **E2E (Reqnroll/Gherkin + Playwright):** buy items → total updates → out-of-stock graying →
  checkout shows correct change → reset; plus a language-switch scenario asserting localized labels.

## 12. Documentation

- This spec: `docs/superpowers/specs/`.
- `docs/architecture/`: C4 (System Context, Container, Component) + a checkout sequence diagram + an
  ER diagram, all as **Mermaid**, linked from the root README.
- `DEPLOY.md` at the root: full deployment/bring-up steps.
- Concise per-component READMEs: `src/backend/README.md`, `src/frontend/README.md`.
- Root README kept lean: overview, quick start (`docker compose up`), links to diagrams and deploy
  doc, assumptions, trade-offs, and "what I'd improve."

## 13. Conventions

- **Commits:** Conventional Commits with scope (e.g. `feat(api): add checkout endpoint`,
  `test(domain): cover change calculator`, `docs: add C4 diagrams`). No AI co-author trailer.
- **Money:** integer cents end to end.
- **Branding:** no company name referenced anywhere.

## 14. What I'd improve (production)

Authentication per seller; a real payment provider behind the existing `IPaymentService` seam;
finite cash-drawer tracking with denomination-aware change; pessimistic locking or stock
reservations under heavy contention; a CI/CD pipeline; multi-event support.
