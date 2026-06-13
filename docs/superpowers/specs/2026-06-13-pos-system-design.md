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
- **Product images** are static assets bundled in the frontend, resolved by the product's
  `ImageKey`, with a generic placeholder fallback when an image is missing.
- **.NET 10** target. **Money is integer cents everywhere; never floating point.**
- **Localization: Estonian + English** (see §8).

> Branding note: the UI uses a generic custom green palette. No company name or brand is referenced
> anywhere in code, docs, or UI.

## 4. Data model

- **Product**: `Id, Name (canonical/base-culture, required), Category (Edible | SecondHand),
  PriceCents, StockQuantity, ImageKey, IsActive, RowVersion`
- **ProductTranslation**: `Id, ProductId, CultureCode, Name` — one row per non-base language.
  Unique constraint on `(ProductId, CultureCode)`. Culture codes are **neutral** (`en`, `et`);
  incoming `Accept-Language` values are matched down to their neutral culture.
- **Order**: `Id, CreatedAtUtc, TotalCents, CashPaidCents, ChangeCents, IdempotencyKey (unique)` +
  lines
- **OrderLine**: `Id, OrderId, ProductId, ProductName (snapshot), UnitPriceCents (snapshot),
  Quantity, LineTotalCents`

**Localized product names are stored in the database.** Each product has a required canonical
`Name` in the base culture (**English, `en`**), plus zero or more `ProductTranslation` rows for
other cultures (e.g. Estonian, `et`). When a requested culture has no translation, the name **falls
back to the canonical `Name`**. The base-culture name is mandatory; all others are optional.

Order lines **snapshot** the **canonical** product name and unit price so historical orders and the
funds-raised report stay correct and language-stable even if a product is later renamed.
`RowVersion` is the optimistic-concurrency token on Product.

## 5. REST API

Follows REST best practices (plural nouns, correct status codes, `ProblemDetails` for errors,
**API versioning**, documented with Swagger/OpenAPI).

**API versioning.** REST endpoints are versioned with **URL-segment versioning** (`/api/v1/...`)
via the `Asp.Versioning.Http` library. The current version is **v1**; supported and deprecated
versions are advertised in the `api-supported-versions` / `api-deprecated-versions` response
headers, and OpenAPI exposes one document per version (e.g. a "v1" Swagger doc). New breaking
changes would ship under `/api/v2` while v1 continues to serve, demonstrating a forward-compatible
contract. The SignalR hub is transport, not REST, so it is not version-segmented.

| Method | Route | Purpose | Responses |
|---|---|---|---|
| GET | `/api/v1/products` | Catalog with price + live stock | 200 |
| GET | `/api/v1/products/{id}` | Single product | 200 / 404 |
| PUT | `/api/v1/products/{id}/stock` | Set second-hand quantity on the day (admin) | 200 / 404 / 400 |
| POST | `/api/v1/orders` | Checkout — lines + cashPaidCents (+ idempotency key) | 201 / 409 / 422 / 400 |
| GET | `/api/v1/orders/{id}` | Order detail incl. change breakdown | 200 / 404 |
| GET | `/api/v1/reports/summary` | Funds raised, items sold | 200 |
| Hub | `/hubs/stock` | SignalR `StockChanged(productId, newQuantity)` | — |

`GET /api/reports/summary` returns: total funds raised (cents), order count, and an items-sold
breakdown per product (quantity and line revenue in cents).

The admin stock endpoint is **unauthenticated by design** — tablets are anonymous (§ seller
identity decision). In production it would sit behind staff authentication; called out as a
conscious trade-off, not an oversight.

Product endpoints resolve the **localized name via the `Accept-Language` request header**, falling
back to the canonical name when no translation exists; the client re-fetches the catalog when the
user switches language (cheap, and stock is live over SignalR anyway). This is standard HTTP content
negotiation rather than leaking every translation to the client.

The **cart is client-side only** — there are no cart endpoints. The server always recomputes totals
authoritatively from current prices at checkout; client-side totals are display only.

Status-code semantics for checkout:
- **409 Conflict** — insufficient stock for a line, or an optimistic-concurrency conflict.
- **422 Unprocessable Entity** — cash paid is less than the total.
- **400 Bad Request** — empty cart, zero/negative quantities, or an unknown product id in a line.
- **201 Created** — success; body includes order id, total, and the change breakdown.

**Machine-readable errors.** All error responses are `ProblemDetails` carrying a language-neutral
`errorCode` and, where relevant, the offending `productId` — never localized prose. The codes:
`out_of_stock`, `concurrency_conflict`, `insufficient_payment`, `empty_cart`, `invalid_quantity`,
`unknown_product`. This lets the client react programmatically — **auto-retry** a
`concurrency_conflict`, **stop and refresh** on `out_of_stock` — and render the user-facing message
from its own `.resx` resources (see §8), keeping the API language-agnostic.

**Idempotent checkout.** `POST /api/orders` accepts a client-generated idempotency key (header
`Idempotency-Key`, a GUID). The key is persisted with the order; a replay with the same key returns
the **original** order (201/200) instead of creating a duplicate or double-decrementing stock —
guarding against double-submits on flaky tablet/phone networks.

## 6. Checkout & concurrency flow

1. Seller taps product images → client cart; running total shown at the bottom (display only).
2. Checkout modal prompts for cash received.
3. `POST /api/orders` with line items + `cashPaidCents` + an `Idempotency-Key` header.
4. Server checks the idempotency key: if an order already exists for it, return that order
   immediately (no new work). Otherwise, in **one DB transaction**: load products → validate stock
   per line (else 409 `out_of_stock`) → validate `cashPaid ≥ total` (else 422
   `insufficient_payment`) → decrement stock → `SaveChanges` with **optimistic concurrency** via
   `RowVersion`. On `DbUpdateConcurrencyException`: reload products, **re-run stock validation**, and
   retry once; the retry succeeds if stock is still sufficient, otherwise returns 409
   `concurrency_conflict`/`out_of_stock`. (This is why the parallel-checkout test sees exactly one
   failure on the last item: after reload the loser observes stock = 0.)
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
- Unknown product id → **404** on `PUT /products/{id}/stock`; **400** (`unknown_product`) when it
  appears in a checkout line.
- Double-submitted checkout (network retry) → idempotency key returns the original order, no
  duplicate or double-decrement.
- Second-hand items start at 0 stock until entered on the day (grayed until then).
- All money handled as integer cents; no floating-point arithmetic.

## 8. Localization

- **Languages: Estonian (`et`) and English (`en`).** English is the default; a language switcher in
  the UI toggles culture, persisted to `localStorage`.
- **Two sources of localized text:**
  - **Static UI chrome** (buttons, checkout/change labels, validation and error messages) — `.resx`
    resource files per culture via `IStringLocalizer` in the Blazor app.
  - **Product names** — stored in the database (canonical `Name` + `ProductTranslation` rows) and
    delivered already-resolved by the API via `Accept-Language` content negotiation, with fallback
    to the canonical English name (see §4, §5).
- Numbers and currency are formatted per the active culture.
- Switching language updates the UI chrome immediately and re-fetches the catalog so product names
  reflect the new culture.
- E2E scenarios include switching language and asserting both localized chrome and localized product
  names (including fallback when a translation is absent).

## 9. Seeding (bonus)

A `seed.json` configuration file holds edible items and second-hand items, each with the canonical
English name, optional translations (e.g. an Estonian name), price, and starting quantity
(second-hand items start at 0). On startup the seeder upserts products and their translations into
the database if the catalog is empty. File path and values are configurable, satisfying the bonus
"read items, quantity and price from a configuration file to database."

## 10. Docker

`docker compose up` brings up the whole system:

- **postgres** — database, healthchecked, named volume for persistence.
- **api** — applies EF migrations and runs the seeder on startup, healthchecked; depends on a
  healthy postgres. (Migrate-on-startup assumes a single api instance, which is the case here;
  multiple instances would need a migration gate.)
- **web** — nginx serving the compiled Blazor WebAssembly bundle; depends on the api.

CORS on the api is configured for the web origin. Connection strings and the seed file path come
from environment/config. `DEPLOY.md` documents the full bring-up.

## 11. Testing strategy

- **Unit (xUnit):** Domain `ChangeCalculator` exhaustively (every cent 0–N, exact/over/zero), stock
  and checkout rules; Application services with **NSubstitute**-mocked ports (payment, repos,
  notifier).
- **Integration (xUnit + WebApplicationFactory + Testcontainers Postgres):** all endpoints against a
  real Postgres, seeding, a **parallel-checkout concurrency test** asserting exactly one 409 on the
  last item, an **idempotency test** (same key replayed → one order, no double-decrement), and
  assertions that error responses carry the expected `errorCode`.
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
