# Design

The durable reference for how BudgetTracker looks, sounds, and behaves. This document governs.

**Ground truth for values is [`budgettracker.client/src/shared/theme/tokens.ts`](../budgettracker.client/src/shared/theme/tokens.ts).**
This document describes and links; it deliberately does not restate hex values, because a second
copy is a copy that drifts. Where a rule can be checked mechanically, the check is named.

> `ui-modernization.md` is the **superseded** migration plan that got us here. It describes a
> design system we did not build. Read it for history, not for direction.

---

## 1. Who this is for

Not an invented archetype. The person is legible from the budget in the database:

| | |
|---|---|
| Net income | **$5,646/month** |
| Core (committed) | **$4,678** across 7 lines |
| Buffer (flex) | **$413.50** across 6 lines |
| Slack | **$554.50 — under 10%** |
| Mortgage alone | **57% of take-home** |

Someone running a deliberate, tight household budget. Housing dominates. The distinguishing habit is
that irregular costs — car registration, home maintenance, health co-pays, and a line literally
named *"Gas/Tolls increase (visiting friends)"* — have been pulled out into a **funded Buffer**
rather than left to ambush the month. This is a person who has already done the hard thinking. The
software's job is to keep the books, not to teach budgeting.

### What follows from that

**Precision over delight.** With ~$554 of monthly slack, a $200 surprise is a third of the cushion.
Never round away something that changes a decision. Amounts on individual transactions carry cents;
only roll-ups and axis labels drop them.

**Buffer is where the anxiety lives.** Core is fixed and boring — the mortgage is the mortgage.
Buffer is where a month goes wrong, and by design it absorbs everything unplanned *and* everything
uncategorised. Surfaces should make Buffer legible, and should never quietly hide what landed there.

**No moralising.** Spending is not a moral failure and the UI must not imply it is. This is already
encoded: `semanticColors.expense` is muted slate, documented as *"so it reads as neutral fact, not
failure."* Red is reserved for genuinely over-plan, not for money leaving.

**Uncategorised is work, not a state.** An uncategorised transaction is invisible to every figure in
the app. Surfaces that show figures must also surface the work that is missing from them.

---

## 2. Voice

The house rule, already written into the code at `MonthHeader.tsx`:

> **Name the meaning, not the mechanism.**

The API reports pacing as `Ahead` / `Behind`, which read backwards to most people — "behind" sounds
like you are under budget. The UI says **"Spending faster than the month"**. Always translate.

### Rules

**Explain the second-order effect.** The most distinctive habit in the existing copy, and worth
keeping: say what it *means*, not just what it *is*.
- "Uncategorized activity is missing from every figure on this page."
- "Imported transactions stay in your history, but no new transactions will sync from Chase afterwards."
- "This removes the plan and all of its lines. This cannot be undone."

**Second person, possessive.** "your bank", "your plan", "until you categorise them". Reserve "we"
for the two cases where the software is the actor: "We'll show your pacing…", "We couldn't reach the API."

**Contractions.** Yes. "Couldn't", not "could not".

**Never leak raw error text into the UI.** Two grammars currently ship:
- ✅ `Could not update the category. Please try again.`
- ❌ `Unable to save budget plan: {error.message}`

The second puts exception strings in front of a person. Use the first shape. Where the server sends
a deliberate, human-readable `error` field, prefer it verbatim — that is different from leaking a
stack message.

**Empty states name the next action.** `No categories found` is a dead end. Say what would fill it.

### House decisions

These currently ship both ways. Pick one and apply it:

| Decision | Rule |
|---|---|
| Spelling | **US English.** `Uncategorized`, `categorize`. (`Auto-categorise` and `uncategorised` are the outliers — they lose.) |
| Ellipsis | The character **`…`**, never three periods. Applies to `Saving…`, `Search payee or notes…`. |
| Sentence case | **Sentence case everywhere** except page titles and proper nouns. `Add plan`, not `Add Plan`. Newer surfaces already do this; the older CRUD screens are the outliers. |
| Terminal punctuation | Buttons and labels: none. Sentences and supporting lines: full stop. Headlines in empty states: none. |
| "No transactions" | One phrasing per context, and it always says why or what next. Six variants currently ship. |
| Destructive confirms | Always the styled `ConfirmModal` with a consequence sentence. Never `window.confirm`. |

### The trail metaphor

`planCopy.ts` bands seven headlines off `pacingDelta` — *"You're cruising in June"*, *"Right on the
trail for June"*, *"Off the trail in June"*. It is the app's only sustained metaphor and it is
warm without being cute. **Keep it**, and keep it confined to the dashboard hero; it should not leak
into transactional surfaces where precision matters more than encouragement.

One line needs rework: *"Tightening up needed in June"* is a subjectless nominalisation sitting
among six subject-led phrases. It reads clunky next to its neighbours.

---

## 3. Visual character

### Colour means something

Colour is never decorative. Six semantic scales (`primary`, `secondary`, `success`, `warning`,
`error`, `info`), each with `main` / `light` / `dark` / `subtle`. The conventions:

- `main` — buttons, links, icons
- `light` / `dark` — hover and emphasis
- `subtle` — tinted background for banners and active nav
- **`dark` flips role between themes.** In light it is deep text on a `subtle` ground; in dark it is
  a light tint, because that same text must invert. This is why button hovers use separate
  `*-hover` tokens rather than a `dark:` override.

**Success green is reserved for income.** It is deliberately excluded from `chartPalette` so a
category series can never be mistaken for money coming in.

**Never write a hex, and never hand-write a CSS variable.** Use Tailwind utilities, or `cssVar()`
from `tokens.ts` for inline styles and SVG. Both are enforced — see §6.

### Type is two systems

**Display (17–30px):** always `font-semibold`, always *negative* tracking, tightening as it grows —
`-0.01em` at 15–17px through `-0.025em` at 30px. Numeric display figures go tightest (`-0.03em`).

**Overline (11–12px):** always `uppercase`, always *positive* tracking (0.04–0.08em), always
`font-semibold text-ink-muted`. This is the single most repeated pattern in the app — table column
headers, field labels, stat labels.

**Body (13–15px):** near-zero tracking, inheriting the global `-0.011em`. 14px is the workhorse.

Prefer the named sizes (`text-sm`, `text-body`, `text-2xs`) over arbitrary `text-[Npx]`. Several
one-off sizes exist (`13.5px`, `14.5px`); do not add more.

### Figures

Every amount, count, and date carries **`.numeric`** — tabular lining figures so columns align on
the decimal, plus optical tightening. Currency goes through
[`shared/utils/format.ts`](../budgettracker.client/src/shared/utils/format.ts): `currency` for
whole-dollar roll-ups, `currencyPrecise` for individual transactions, `signedAmount` where direction
matters.

*Known inconsistency:* several components still use bare `tabular-nums` without `.numeric`, which
loses the tracking.

### Surface and shape

Radii **clamp at 12px** — `xl`, `2xl` and `3xl` all collapse there. Anything larger reads soft.

Cards use a **hairline ring, not a border**, plus a soft wide shadow. Shadows are two-layered (a
tight contact shadow and a wide soft one) and their ink is a themeable channel — navy in light,
near-black in dark, because a navy shadow on a dark ground reads as a smudge.

In dark mode the page sits *below* the card surface: cards lift by a tone step and a border, because
a shadow does no work on a dark ground.

---

## 4. Interaction character

**Inline over modal.** The default posture. Transactions categorise and annotate in place; the
transaction dialog was removed rather than restyled. Reach for a modal only when the action needs
focus or confirmation.

**Confirm only what is destructive or irreversible.** Deleting a plan and its lines, replacing a
bank connection. Not saving, not categorising. Every confirm states the consequence — including what
*survives*, which is often the reassuring part.

**Work in bulk.** Anything a person might do to 200 rows needs multi-select. Categorising through a
modal one row at a time is the failure mode this page was rebuilt to remove.

**Motion is feedback, not decoration.**

| Duration | Use |
|---|---|
| `120ms` | colour and hover — the workhorse |
| `160ms` | transforms, and every *exit* |
| `240ms` | overlays, and every *enter* |

Entering uses `ease-out-soft`, leaving uses `ease-in-soft`. No spring physics, no Framer Motion.

**One focus treatment.** `.focus-ring` — a 3px primary ring at 25%, keyboard-only via
`:focus-visible`. Applied to everything interactive. Do not invent a second.

---

## 5. Components

Everything comes from [`shared/components/ui`](../budgettracker.client/src/shared/components/ui) via
the barrel. Direct MUI/emotion imports are banned by ESLint; MUI is gone.

**The split worth knowing:** form-bound primitives take a react-hook-form `control`
(`Input`, `Select`, `Checkbox`); standalone ones take plain `value`/`onChange`
(`InlineSelect`, `InlineCheckbox`). Reach for the inline pair inside table rows and toolbars, where
there is no form.

| Need | Use |
|---|---|
| Page heading | `PageHeader` |
| Any panel | `Card` (`outlined` default; `padding` none/sm/md/lg) |
| Status word | `Badge` — 6 colours × 3 variants |
| Tabular data | `Table` — handles loading skeletons and empty message |
| Expand in place | `Collapsible` (controlled; you own the trigger) |
| Focused task | `Modal`; `ConfirmModal` for destructive |
| Segmented choice | `ToggleGroup` |
| Progress / ratio | `Progress`; `Gauge` for a headline figure |

New primitives belong in `shared/components/ui`, named plainly — `Button`, not `BudButton`. The
import path conveys ownership.

---

## 6. What is enforced

Mechanical, in `npm test`:

- **Token parity** — every Tailwind colour utility must be a `rgb(var(--bud-*) / <alpha-value>)`
  reference. Catches someone replacing the token import with literals.
- **No hardcoded hex** anywhere in `src/` outside `shared/theme`. The allowlist is empty and must
  stay empty.
- **No undefined CSS variables** — every `var(--…)` must be `--bud-*` or `--radix-*`. Added after
  `var(--color-success)` shipped: an unresolvable `var()` silently invalidates the declaration, so
  all four spend-ramp states rendered the same indigo bar.

By ESLint: no `@mui/*` or `@emotion/*` imports.

**Advisory, could be mechanised later:** currency must go through `shared/utils/format.ts`; every
figure carries `.numeric`; prefer named font sizes over arbitrary `text-[Npx]`.

---

## 7. Specced but not built

Fenced off deliberately. **These are not facts about the app.**

- **The four-step `status.*` ramp.** `ui-modernization.md` specs `status.on-pace` / `warn-low` /
  `warn-high` / `over`. No such tokens exist. The logic exists and is tested
  (`planGrouping.ts` → `spendStatus`), but the page approximates the ramp with `success`,
  `warning`, `warning-dark` and `error`. Building the real tokens is a design-system change.
- **The 8-hue category palette** (`cat.1`–`cat.8`). Never existed in any form. `chartPalette` uses
  four semantic tokens plus their 65% tints instead.
- **A global `prefers-reduced-motion` rule.** Only 7 per-class `motion-reduce:` usages exist, all in
  `TopNav.tsx`. Every keyframe animation is currently ungated.
- **Named primitives that were never built:** `PageShell`, `Section`, `EmptyState`, `StatusPill`,
  `Stat`. `AppShell` and `Badge` cover two of them; the rest are open.

---

## Related

- [Frontend Architecture](frontend-architecture.md) — feature-slice structure.
- [Transactions Page](transactions-page.md) — the reference implementation of this document.
- [UI Modernization](ui-modernization.md) — **superseded.** History and the Copilot research.
