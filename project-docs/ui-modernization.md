# UI Modernization

This document captures a frontend audit of `budgettracker.client` and a phased roadmap for moving the UI toward a Copilot Money-style design language: premium minimalism, restrained color, generous whitespace, and tabular currency typography. It is paired with a token spec so work can be staged behind primitives instead of rewritten page-by-page.

## 1. Executive summary

BudgetTracker's frontend works but reads as a developer prototype. Two parallel styling systems (MUI's theme and Tailwind utilities) are wired in isolation, the only color tokens that exist live in an MUI `createTheme` call that Tailwind cannot see, and every page reaches for raw `text-gray-*` or `grey.700` literals because there is nothing else to reach for. There are no shared layout primitives, no real skeletons, no dark mode, and no semantic status colors — overspend is signalled by a `#C97B4A` literal hard-coded in a chart helper. The goal is not a redesign; it is to install the design system that should have been there from the start and re-skin existing screens through it.

**What changes:**
- A single token source feeds both MUI and Tailwind, with light and dark values defined up front.
- Eight shared primitives (`PageShell`, `PageHeader`, `Section`, `Card`, `Stat`, `Skeleton`, `EmptyState`, `StatusPill`) absorb the per-page styling.
- Semantic color scales replace ad-hoc grays and the orphan clay literal.
- Currency display gets tabular-nums and a proper display size.
- Dark mode ships as a flip of the token provider, not a per-component rewrite.

## 2. Current state audit

### Theme is single-mode and Tailwind cannot see it

[main.tsx](../budgettracker.client/src/main.tsx) defines an MUI `createTheme` with palette keys `forestBlack #100B00`, `leaf #85CB33`, `parchment #EFFFC8`, `mist #A5CBC3`, and `bark #3B341F`. Mode is locked to `'light'`. `shape.borderRadius` is `12`. The `MuiCard` override sets `elevation: 0`, a `alpha('#ffffff', 0.7)` background, and a `0.45`-alpha mist border. These values exist only inside the MUI runtime — they are not exported, not consumed by Tailwind, and not available as CSS variables.

[tailwind.config.js](../budgettracker.client/tailwind.config.js) has `theme: { extend: {} }`. Every Tailwind class in the codebase therefore resolves against the default palette, which is why feature files reach for `text-gray-500`, `bg-red-50`, `border-gray-200` and similar — there is nothing else available.

### MUI `sx` and Tailwind `className` collide on the same elements

[Layout.tsx](../budgettracker.client/src/shared/components/Layout.tsx) is the canonical example. The AppBar uses `bg: 'white'` via `sx`, then borders are drawn with both an `sx` value of `borderColor: '#e5e7eb'` and Tailwind classes elsewhere on the page using `bg-gray-50`. Nav items reach into `grey.700`, `grey.600`, `grey.100`, and `grey.900` directly from the MUI palette. There is no single source of truth for "what color is a divider."

### Semantic colors leak into chart code

[chartTheme.ts](../budgettracker.client/src/features/dashboard/utils/chartTheme.ts) hard-codes `#C97B4A` as the overspend color. That value is not part of the MUI palette and not part of Tailwind's defaults. It only exists inside the chart helper, which means dashboards and tables that need the same semantic color have to either re-import the constant or invent their own. There is no `status.over` token to bind to.

### Pages are built ad-hoc on raw MUI + Tailwind

- [DashboardPage.tsx](../budgettracker.client/src/features/dashboard/pages/DashboardPage.tsx) uses a hero band plus a Tailwind grid.
- [TransactionsPage.tsx](../budgettracker.client/src/features/transactions/pages/TransactionsPage.tsx) uses a calendar-left / detail-right split built on `@mui/x-date-pickers`.
- [BudgetPlansPage.tsx](../budgettracker.client/src/features/budget-plans/pages/BudgetPlansPage.tsx) stacks raw MUI `<Card>` elements containing tables.
- [SettingsPage.tsx](../budgettracker.client/src/features/settings/pages/SettingsPage.tsx) mixes Tailwind border utilities with MUI buttons.

Each page reinvents its container chrome.

### Missing primitives

There is no shared `Card` shell, no `PageShell`, no `PageHeader`, no `Section`, no `Stat`, no `EmptyState`, no `StatusPill`. There is no real `Skeleton` — [DashboardLoadingState.tsx](../budgettracker.client/src/features/dashboard/components/DashboardLoadingState.tsx) renders a spinner. Charts are MUI x-charts (`BarChart`, `LineChart`, `Gauge`, `SparkLineChart`), which is fine, but their colors are not tokenized.

### Accessibility and motion gaps

No dark mode. No `prefers-reduced-motion` handling. Focus rings are absent or default-browser. `aria-current` is not set on the active nav item in `Layout.tsx`. Touch targets in the AppBar are MUI defaults and have not been audited against 44px.

### What is good

Forms are already on `react-hook-form` + `zod`, with shared schemas in [shared/validation/](../budgettracker.client/src/shared/validation/). Server state is on React Query. The feature-first folder layout from `frontend-architecture.md` is mostly in place. None of those change for this work.

## 3. Target: Copilot Money design language

Copilot Money is the reference point. Distilled cues — informed by [copilot.money](https://www.copilot.money/), the design lead's portfolio at [mattstromawn.com](https://mattstromawn.com/projects/copilotmoney/), and third-party reviews:

- **Premium minimalism.** Very few colors on screen at once. Surfaces are off-white in light mode and off-black in dark mode (true `#000` halos against bright accents). Borders are low-contrast. Shadows are barely there.
- **Generous whitespace, high information density.** Cards are roomy inside but pack a lot of relevant data per screen. Padding scales with hierarchy.
- **Large bold currency numbers.** Hero amounts on the dashboard read as a display typeface. Currency always uses `font-variant-numeric: tabular-nums` so columns of numbers align.
- **Restrained semantic ramp for budget status.** Copilot uses a 4-step ramp documented at [help.copilot.money](https://help.copilot.money/en/articles/10309907-dashboard-line-colors): green for on-pace, light orange for 0–20% over, dark orange for >20% over, red for over budget. We adopt the same shape with our own hues.
- **Apple-native restraint.** SF Pro on iOS; Inter is the cross-platform stand-in we already use.
- **Card-based dashboard.** Each card is a self-contained module — stat, chart, breakdown — with its own header, optional action, and content.
- **Motion as feedback, not decoration.** Animated gauges, smooth chart transitions on month change, tap-to-expand. We will reach CSS transitions before Framer Motion.

What we are not copying: Copilot's exact palette, their iOS-only navigation, or their proprietary categorization UI. We keep `leaf #85CB33` as the BudgetTracker brand accent — inspiration, not impersonation.

## 4. Proposed design tokens

Tokens live in `budgettracker.client/src/shared/design/tokens.ts`, exported as a JS object (consumed by `main.tsx` to build the MUI theme) and as CSS custom properties on `:root` and `[data-theme="dark"]` (consumed by Tailwind via `tailwind.config.js`).

### 4.1 Color

| Token | Light | Dark | Notes |
|---|---|---|---|
| `bg.canvas` | `#FAFAF7` | `#0B0D0C` | Page background. Off-white / off-black to avoid haloing. |
| `bg.surface` | `#FFFFFF` | `#15181A` | Card and panel background. |
| `bg.surface-elevated` | `#FFFFFF` | `#1C2023` | Modals, popovers; paired with `e1` shadow in light. |
| `fg.primary` | `#100B00` | `#F5F5F0` | Existing `forestBlack` retained for light. |
| `fg.secondary` | `#3B341F` | `#B8B8AE` | Existing `bark` retained for light. |
| `fg.muted` | `#7A7466` | `#7E8278` | Captions, helper text. |
| `border.subtle` | `rgba(16,11,0,0.08)` | `rgba(245,245,240,0.08)` | Default card and divider border. |
| `border.strong` | `rgba(16,11,0,0.16)` | `rgba(245,245,240,0.16)` | Focused or selected state. |
| `accent.primary` | `#85CB33` | `#9BDB4F` | Existing `leaf`; brighter in dark for contrast. |
| `status.on-pace` | `#2E9E3F` | `#4ECB60` | Mirrors Copilot's green. |
| `status.warn-low` | `#E89E3C` | `#F4B266` | 0–20% over budget. |
| `status.warn-high` | `#D67232` | `#E89559` | >20% over budget. |
| `status.over` | `#C8423A` | `#E36058` | Over budget. Retires the orphan `#C97B4A`. |

**Category palette.** Eight muted, harmonious hues for category chips, donut slices, and bar segments. Names are semantic to the slot, not the hue, so categories can be reordered without breaking other consumers.

| Token | Light | Dark |
|---|---|---|
| `cat.1` | `#7BA89A` | `#94BFAF` |
| `cat.2` | `#D9A05B` | `#E5B57A` |
| `cat.3` | `#A37FB8` | `#BD9CD0` |
| `cat.4` | `#5C8DB0` | `#7BA8C9` |
| `cat.5` | `#C26D7A` | `#D88B97` |
| `cat.6` | `#6E9B5A` | `#8DBA79` |
| `cat.7` | `#B59B5F` | `#CFB67C` |
| `cat.8` | `#6F7BA0` | `#8E9ABF` |

### 4.2 Typography

| Token | Size / line-height | Weight | Use |
|---|---|---|---|
| `display` | 48 / 56 | 700 | Hero net cashflow on dashboard. |
| `h1` | 32 / 40 | 700 | Page titles in `PageHeader`. |
| `h2` | 24 / 32 | 600 | Section headers. |
| `h3` | 18 / 26 | 600 | Card titles. |
| `h4` | 16 / 24 | 600 | Sub-section headers within cards. |
| `body` | 14 / 22 | 400 | Default text. |
| `caption` | 12 / 18 | 400 | Secondary metadata, helper text. |
| `mono-numeric` | inherit | inherit | `font-variant-numeric: tabular-nums` applied to all currency. |

Inter remains the family. SF Pro is preferred when available via the system font stack.

### 4.3 Spacing

Tailwind's default 4px integer scale: `0, 4, 8, 12, 16, 24, 32, 48, 64, 96`. Tokens are exposed as `space.0` through `space.96`. No half-pixel values.

### 4.4 Radii

| Token | Value | Use |
|---|---|---|
| `radius.sm` | 6 | Inputs, tags. |
| `radius.md` | 10 | Buttons, pills with text. |
| `radius.lg` | 14 | Cards, modals. |
| `radius.pill` | 999 | `StatusPill`, avatars. |

### 4.5 Shadows

| Token | Light | Dark |
|---|---|---|
| `shadow.e0` | none | none |
| `shadow.e1` | `0 1px 2px rgba(16,11,0,0.04), 0 1px 1px rgba(16,11,0,0.06)` | `0 1px 2px rgba(0,0,0,0.4)` |
| `shadow.e2` | `0 8px 24px rgba(16,11,0,0.10)` | `0 8px 24px rgba(0,0,0,0.5)` |

### 4.6 Motion

| Token | Value |
|---|---|
| `duration.fast` | `120ms` |
| `duration.base` | `200ms` |
| `duration.slow` | `320ms` |
| `easing.standard` | `cubic-bezier(.2, .8, .2, 1)` |

All motion gated by `@media (prefers-reduced-motion: reduce)` — durations collapse to `0ms` and easings become `linear`.

## 5. Roadmap

Seven phases. Each phase is intended as a single PR that ships a visible delta. Phases land in order; later phases depend on the primitives created in earlier ones.

### Phase 1 — Foundations

**Goal:** Establish a single token source that both MUI and Tailwind consume, and prepare for dark mode without enabling it.

- Create [shared/design/tokens.ts](../budgettracker.client/src/shared/design/tokens.ts) exporting the spec in §4 as a typed object and emitting CSS custom properties on `:root` and `[data-theme="dark"]`.
- Extend [tailwind.config.js](../budgettracker.client/tailwind.config.js) to map `colors`, `spacing`, `borderRadius`, `boxShadow`, and `fontSize` to the CSS variables. Enable `darkMode: ['class', '[data-theme="dark"]']`.
- Refactor [main.tsx](../budgettracker.client/src/main.tsx) so `createTheme` reads from the token export instead of hard-coded hex strings. Keep light mode as default.
- Add a `<ColorModeProvider>` that owns the `data-theme` attribute on `<html>` and persists the choice to `localStorage`. No UI toggle yet.
- Retire the `#C97B4A` literal in [chartTheme.ts](../budgettracker.client/src/features/dashboard/utils/chartTheme.ts) by reading `status.over` from tokens.

**Ships as:** No visible change. Audit pass: confirm that every existing screen still renders identically and that DevTools shows the new CSS variables on `<html>`.

### Phase 2 — Shared primitives

**Goal:** Build the eight primitives that subsequent phases will compose against, in isolation from any feature.

- Create [shared/components/ui/](../budgettracker.client/src/shared/components/ui/) containing:
  - `PageShell` — outer page chrome: max-width container, canvas background, vertical rhythm.
  - `PageHeader` — title, optional subtitle, optional action slot.
  - `Section` — labelled grouping with consistent vertical spacing.
  - `Card` — token-driven surface replacing the ad-hoc MUI `<Card>` overrides.
  - `Stat` — large currency or count with caption, supports trend indicator.
  - `Skeleton` — shimmer placeholder matching token surfaces.
  - `EmptyState` — illustration slot, headline, body, optional action.
  - `StatusPill` — small pill bound to `status.*` and `cat.*` tokens.
- Each primitive accepts variants via discriminated unions, not loose `className` overrides.

**Ships as:** No route changes. Primitives are reachable from the existing pages but not yet used; visual delta only when a phase below adopts them.

### Phase 3 — App shell

**Goal:** Replace the AppBar / sidebar chrome with a token-driven layout and fix the accessibility gaps.

- Rewrite [Layout.tsx](../budgettracker.client/src/shared/components/Layout.tsx) on `PageShell` and tokens. Remove every `bg-gray-50`, `#e5e7eb`, and `grey.700`-style literal.
- Set `aria-current="page"` on the active nav item.
- Add visible focus rings using `border.strong` plus a 2px offset.
- Add a dark-mode toggle button in the top chrome. It flips `<ColorModeProvider>`; dark surfaces will still be rough until Phase 7.

**Ships as:** A noticeably cleaner top bar and side nav, focusable via keyboard with visible rings, and a working dark-mode toggle (cosmetically incomplete inside content areas).

### Phase 4 — Dashboard pass

**Goal:** Re-skin the dashboard on the primitives and introduce the Copilot-style hero treatment.

- [DashboardPage.tsx](../budgettracker.client/src/features/dashboard/pages/DashboardPage.tsx) composes `PageShell` + `PageHeader` + a grid of `Card`s.
- [PlanStoryHero.tsx](../budgettracker.client/src/features/dashboard/components/PlanStoryHero.tsx) uses the `display` type token for the net cashflow figure with `mono-numeric`.
- [CashflowWaterfall.tsx](../budgettracker.client/src/features/dashboard/components/CashflowWaterfall.tsx), [BucketBreakdown.tsx](../budgettracker.client/src/features/dashboard/components/BucketBreakdown.tsx), and [CategoryDrillCard.tsx](../budgettracker.client/src/features/dashboard/components/CategoryDrillCard.tsx) bind chart fills to `status.*` and `cat.*` tokens through `chartTheme.ts`.
- [DashboardLoadingState.tsx](../budgettracker.client/src/features/dashboard/components/DashboardLoadingState.tsx) replaces the spinner with the `Skeleton` primitive shaped to match the real layout.

**Ships as:** A dashboard that reads as a finished product — hero amount in display type, cards on a consistent grid, proper skeletons during load.

### Phase 5 — Transactions and Budget Plans

**Goal:** Apply the same treatment to the two heaviest data screens.

- Rethink [TransactionsPage.tsx](../budgettracker.client/src/features/transactions/pages/TransactionsPage.tsx) and [TransactionTable.tsx](../budgettracker.client/src/features/transactions/components/TransactionTable.tsx) as a grouped list — Today / Yesterday / earlier — with `mono-numeric` currency columns and `StatusPill` for category. The calendar pane stays but is restyled on tokens.
- [BudgetPlansPage.tsx](../budgettracker.client/src/features/budget-plans/pages/BudgetPlansPage.tsx) and [BudgetPlanCard.tsx](../budgettracker.client/src/features/budget-plans/components/BudgetPlanCard.tsx) move onto the shared `Card`.
- Progress visuals on budget plans bind to the 4-step semantic ramp from §4.1.

**Ships as:** Transactions read as a feed, not a spreadsheet. Budget plan cards match the dashboard cards visually.

### Phase 6 — Settings, auth, and polish

**Goal:** Clean up the remaining routes and address motion and contrast holistically.

- Move [SettingsPage.tsx](../budgettracker.client/src/features/settings/pages/SettingsPage.tsx) and the auth routes under `PageShell` + `PageHeader`.
- Apply `prefers-reduced-motion` site-wide via a global CSS rule that collapses `duration.*` tokens to `0ms`.
- Contrast audit: verify all `fg.*` / `bg.*` pairs meet WCAG AA, with focus on muted text on canvas.
- Replace any remaining raw `text-gray-*` / `bg-red-*` literals discovered during the audit.

**Ships as:** Settings and auth no longer look like a different application. A user with reduced-motion enabled gets a still UI.

### Phase 7 — Dark mode v1

**Goal:** Make the dark-mode toggle from Phase 3 actually pleasant.

- Walk every primitive and feature card, checking that mode-dependent surfaces (selected nav, hover states, chart gridlines, skeleton shimmer) read correctly in dark.
- Verify status colors in dark using a real screen, not just contrast ratios.
- Confirm scrollbars, focus rings, and selection colors all inherit from tokens.

**Ships as:** A dark mode that does not feel like a debug build. Toggle persists across reloads.

## 6. References and inspiration

- [copilot.money](https://www.copilot.money/) — marketing site, current product surface.
- [mattstromawn.com/projects/copilotmoney](https://mattstromawn.com/projects/copilotmoney/) — Copilot design lead's portfolio with annotated design rationale.
- [help.copilot.money — Dashboard line colors](https://help.copilot.money/en/articles/10309907-dashboard-line-colors) — the 4-step semantic budget ramp.
- [Copilot on the App Store](https://apps.apple.com/us/app/copilot-track-budget-money/id1447330651) — current screenshots and motion samples.
- [The Penny Hoarder — Copilot Money review](https://www.thepennyhoarder.com/budgeting/budgeting-copilot-money-review/) — third-party narrative on the product feel.
- [UX Bootcamp — Copilot UX/UI audit](https://bootcamp.uxdesign.cc/ux-ui-audit-4-improvements-for-the-copilot-app-57e9f8e4ac20) — counterpoints and weak spots in Copilot's UI worth avoiding.

## 7. Non-goals

- No new features. The product surface is identical; only presentation changes.
- No backend or API changes. No DTO renaming, no new endpoints, no contract churn.
- No iOS port. Native mobile work is owned by `mobile-strategy.md`.
- No Storybook. If silvio scopes it later it lands as Phase 8.
- Not adopting Copilot's exact palette. `leaf #85CB33` remains the brand accent.
- No Framer Motion. CSS transitions are sufficient through Phase 6; spring physics can come later if needed.
