# Design System — Pos.Web

> **Authoritative constraints.** All UI components, layouts, and code generation for the frontend
> **must** adhere to the tokens below. Do not deviate, do not introduce ad-hoc colors, and do not
> hard-code hex values outside the token definitions. New surfaces inherit these tokens.

## Theme

Modern, minimalist bakery / e-commerce aesthetic. High negative space, generous rounding, clean
typography, calm and crisp.

## Color tokens (60 / 30 / 10)

| Role | Token | Hex | Usage budget | Applies to |
|---|---|---|---|---|
| Dominant | `--color-surface` | `#F9FAFB` | ~60% | Page backgrounds, cards |
| Secondary | `--color-structure` | `#E5E7EB` | ~30% | Layout structure, dividers, borders, inactive chrome |
| Accent | `--color-accent` | `#059669` | ~10% | Primary CTAs, active/selected states, key highlights |
| Text | `--color-text` | `#1E293B` | — | All body & heading typography (crisp readability) |

**Crisp Off-White** `#F9FAFB` · **Cool Gray** `#E5E7EB` · **Mint Emerald** `#059669` · **Deep Slate** `#1E293B`

Rules:
- The accent (`#059669`) is reserved for the **10%** — primary CTAs and active states only. Don't paint
  large areas with it.
- Default text color is **Deep Slate `#1E293B`**, never pure black.
- Backgrounds and cards are **Crisp Off-White `#F9FAFB`**, never pure white.

### Permitted derived & semantic colors

The four tokens above are the palette. A small, fixed set of in-family derivatives is allowed where the
core four can't carry meaning — do **not** add others:

| Purpose | Value | Family |
|---|---|---|
| Muted text (labels, captions) | `#64748B` | Slate (text) |
| Faint text / disabled icons | `#94A3B8` | Slate (text) |
| Pressed/hover accent | `#047857` | Emerald (accent) |
| Subtle accent fill (hover wash, change panel) | `#ECFDF5` / border `#A7F3D0` | Emerald (accent) |
| Error / destructive | `#DC2626`, tint `#FEF2F2` | Semantic only |

### Semantic status colors (toasts, alerts, validation)

Status meaning is carried by these tokens — used for the icon and the left edge of a notification,
never as large fills. Notification **text stays dark** (`#1A1A1A`) for contrast; the color signals type.

| Meaning | Token | Hex |
|---|---|---|
| Error / destructive | `--error` | `#FF4D4F` |
| Warning / caution | `--warning` | `#F59E0B` |
| Success / confirm | `--success` | `#059669` (= accent green) |
| Info / notification | `--info` | `#3B82F6` |
| Notification text | `--content` | `#1A1A1A` |

Derived for legible small text/fills where the solid token is too low-contrast: `--warning-tint`
(`#FEF3C7` fill), `--warning-ink` (`#92400E` text). These are mirrored into the MudTheme palette
(`Error`/`Warning`/`Success`/`Info`) so MudBlazor snackbars and alerts inherit them.

### Notification style — Translucent Glass Toast

All toasts/alerts share one treatment (see `.mud-snackbar` / `.mud-alert` in `app.css`):

- **Background:** `rgba(255, 255, 255, 0.85)` with `backdrop-filter: blur(20px)`.
- **Border:** a clean **1px border on the LEFT edge only**, in the solid token for the type
  (e.g. `--error` for an error toast).
- **Text & icons:** dark neutral text (`--content`); the **icon** uses the solid type token.

Pick severity by meaning, not severity-as-color: out-of-stock/over-stock is a **warning** (amber), a
completed sale is **success**, a failed checkout/login is **error**.

## Typography

- Color: **Deep Slate `#1E293B`** for crisp readability.
- Clean, legible type. Clear hierarchy via weight and size, not color or decoration.

## Aesthetics

- **High negative space** — let layouts breathe; prefer padding over dense packing.
- **Rounded card borders** — use a large radius (e.g. `rounded-2xl` / `border-radius: 1rem`).
- Clean typography, minimal ornamentation, subtle borders/dividers in Cool Gray over heavy shadows.

## Media assets

- **Product imagery is local JPG**, served from `wwwroot/images/` as `images/{imageKey}.jpg` (one file
  per product `ImageKey`, e.g. `brownie.jpg`, `cakepop.jpg`). We **switched from SVG icons to JPG
  photos** — full-bleed `object-fit: cover` thumbnails. The legacy `.svg` icons remain only as
  historical assets and are no longer referenced.
- A per-product **emoji is the graceful fallback** when a photo is missing (`ProductCard` swaps to it
  via the `<img onerror>` handler), so a missing/oversized file never breaks the grid.
- For throwaway mockups/prototypes (not the app), high-quality Unsplash bakery/food URLs are fine; the
  shipping app must use the local JPG assets above.

## Token reference (CSS custom properties)

```css
:root {
  --color-surface:   #F9FAFB; /* 60% — backgrounds, cards */
  --color-structure: #E5E7EB; /* 30% — layout, borders, dividers */
  --color-accent:    #059669; /* 10% — primary CTAs, active states */
  --color-text:      #1E293B; /* typography — Deep Slate */
  --radius-card:     1rem;    /* rounded-2xl equivalent */
}
```

When working in MudBlazor, map the MudTheme palette to these tokens (Primary → `#059669`,
Background/Surface → `#F9FAFB`, lines/dividers → `#E5E7EB`, TextPrimary → `#1E293B`).
