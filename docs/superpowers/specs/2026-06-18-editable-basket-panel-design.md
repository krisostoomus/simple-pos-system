# Editable basket panel on the sale screen

**Date:** 2026-06-18
**Status:** Approved

## Problem

The sale screen lets sellers add items by tapping product cards (quantity = taps), but there is no way
to remove items one by one or correct a mistaken quantity short of clearing the whole cart with Reset.
`CartService.Remove(productId)` already exists and decrements a line; it is simply not wired to any UI.

## Goal

Give sellers an editable view of the current cart on the sale screen: see each line, adjust its
quantity up/down one at a time, and remove a whole line — without leaving the screen or clearing
everything.

## Design

### Behavior

A collapsible bottom sheet on the sale screen (`/`). A persistent handle strip —
**`🛒 N items · €total ⌃`** — sits directly above the existing checkout bar. Tapping it slides up a
scrollable list of the cart's lines; tapping again (chevron flips to `⌄`) collapses it. Collapsed is
the default. When the cart is empty the handle is hidden (consistent with the already-disabled
Reset/Checkout buttons).

Each line shows: **thumbnail/emoji · name · unit price · `−  qty  +` stepper · line total · `✕`**.

- `−` → `Cart.Remove(id)` (line vanishes at qty 0)
- `+` → `Cart.Add(id)`, disabled when qty has reached available stock (reuses the `Remaining` logic
  from `ProductCard`: `StockQuantity - quantity`)
- `✕` → `Cart.RemoveLine(id)` removes the whole line at once

### Components & data flow

- **`CartService`** gains one method: `RemoveLine(int productId)` → removes the key and fires
  `Changed`. (`Add`, `Remove`, `Count`, `IsEmpty`, `QuantityOf` already exist.)
- **New `Components/BasketPanel.razor`** — presentational, no service injection (cleanly
  bUnit-testable). Parameters: the line view-models (product + quantity + line-total cents), the
  formatted total, and `EventCallback<int>` `OnAdd` / `OnRemove` / `OnRemoveLine`. Owns only its own
  expanded/collapsed boolean.
- **`Sale.razor`** already owns `_products`, `_priceById`, and `Cart`. It builds the line list by
  joining `Cart.Quantities` with `_products` (for name, unit price, stock), renders `<BasketPanel>`
  between the product grid and the checkout bar, and wires the callbacks to `Cart.Add` /
  `Cart.Remove` / `Cart.RemoveLine`. It already re-renders on `Cart.Changed`, so stepper taps refresh
  live; SignalR `StockChanged` updates flow through `_products` and disable `+` automatically.

A small line view-model (e.g. `BasketLine` record: `ProductModel Product`, `int Quantity`,
`int LineTotalCents`) is built in `Sale.razor` and passed to the panel.

### Styling & i18n

- New CSS classes in `wwwroot/css/app.css` following the existing `pos-*` convention
  (`pos-basket-*`). The sheet sits above `.pos-checkout-bar`, animated with a CSS `max-height` /
  transform transition.
- New keys in **both** `Resources/UiStrings.en.resx` and `UiStrings.et.resx`: `Basket`, `ItemsCount`
  (format string, e.g. `"{0} items"`), `RemoveLine`, and aria-labels for the `+` / `−` / `✕` buttons.
  Money formatted via the existing `Money.FormatEuro`.

### Tests

- **bUnit** (`Pos.Web.Tests`): `BasketPanel` renders one row per line; `−` / `+` / `✕` invoke the
  correct callbacks with the right product id; `+` is disabled when quantity equals available stock;
  an empty line list renders no rows / no handle.
- **`CartService`** unit test for `RemoveLine` (removes the whole line; no-op for an absent id).
- Existing E2E purchase flow must stay green; optionally extend one scenario to decrement a line.

## Out of scope

- No backend changes — the server still recomputes totals authoritatively at checkout.
- No persistence of the cart across page reloads.
- No direct quantity typing (steppers only).
- No "remove" affordance added to the product cards themselves (the basket panel is the single
  editing surface).
