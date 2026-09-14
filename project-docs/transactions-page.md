# Transactions Page

A guide to the **Transactions** experience: how to review what your bank imported, categorise it,
and read it against your budget plan.

## Overview

Transactions come from Plaid. There is no manual entry — the page exists to *review* an imported
ledger, not to type one in. The two things you can change on a transaction are its **category** and
its **note**; everything else is what the bank reported.

The page works one month at a time. A month switcher in the header drives every view, and the
plan-vs-actual figures follow it — browsing to June shows June's performance, not today's.

Three views, behind the toggle at the top:

| View | What it is for |
|---|---|
| **Plan** (default) | The month grouped the way the budget is structured: Core, then Buffer, then by category. |
| **List** | A flat, searchable, filterable list. For "where did that charge go". |
| **Calendar** | The day-by-day ledger. For "what happened on the 14th". |

## Month header

Shows the month under review, the plan governing it, and how the month is tracking:

- **Planned** — the sum of the plan's expense lines, using each line's monthly equivalent.
- **Actual** — what has been spent so far.
- **Remaining** (or **Over plan**) — the difference.
- A progress bar with a marker showing how far through the month you are. Bar left of the marker
  means you are spending slower than the calendar.

The pace badge says what is happening in plain terms — "Spending faster than the month" — rather
than the API's `Ahead`/`Behind`, which read backwards to most people.

If no plan governs the month yet, the header says so and the spending below is still grouped by
category.

## Plan view

The heart of the page. Categories are grouped into the two buckets the budget is built from:

- **Core** — committed spending you have signed up for.
- **Buffer** — flex spending. **Anything unplanned or uncategorised lands here too**, which is the
  point: you commit to Core, and everything else eats your Buffer.

Each category row shows actual against planned with a progress bar that ramps through four steps —
on pace, slightly over, well over, and over budget — plus an "over by" badge once actual exceeds
planned. Categories are ordered worst-overspend-first, so the row that needs attention is at the
top. Expanding a row lists that category's transactions.

**Uncategorised is pinned above both buckets.** Those transactions are missing from every figure on
the page and on the dashboard until they are categorised, so they are surfaced rather than buried.

A category with a plan line but no spend still appears — an untouched budget is information.

## Categorising

Three ways, in increasing order of speed:

1. **Inline** — the category dropdown on any transaction row.
2. **One-click suggestion** — imported rows carry Plaid's suggested category. When one of your
   categories claims that Plaid value, a "Use *Category*" button appears. Plaid's raw suggestion is
   kept even after you override it.
3. **Bulk** — select rows with their checkboxes and set a category for all of them at once.

Map a category to a Plaid taxonomy value in the **Categories** page ("Auto-categorise imports as").
Once mapped, matching transactions are categorised automatically on the next sync, and a re-sync
never overwrites a category you chose yourself.

## Notes

Click the note affordance on any row to add or edit a note inline. Enter saves, Escape cancels.
Notes are capped at 1000 characters.

## Signed amounts

Amounts are stored **signed**: negative for outflows, positive for inflows. Imported transactions
are only ever `Expense` or `Income` — Plaid's sign convention is inverted on import.

In the UI a transaction counts as an **inflow** if it is `Income`, or an `Adjustment` with a
positive amount. Inflows render green with a `+`; everything else renders with a `-`.

Totals follow the same rule: income and outflow are shown as non-negative magnitudes, and the net
figure carries the sign.

## Gotchas

- **Only category and notes are editable.** Amount, date, payee and account are what the bank
  reported. The server rejects any attempt to change them, not just the UI.
- **Imported transactions cannot be deleted.** Disconnect the bank to remove them.
- **Buffer absorbs the unplanned.** A category with no plan line shows as "unbudgeted" and its spend
  counts toward Buffer, matching how budget analysis computes it.
- **Annual plan lines are compared monthly.** A $2,004/yr maintenance line is $167/month; the page
  compares against the monthly equivalent, never the annual amount.
- **Uncategorised spend is invisible to the plan figures.** That is why it is pinned to the top.

## Related documentation

- [Frontend Architecture](frontend-architecture.md) — the feature-slice structure these components follow.
- [Database Schema (ERD)](database-schema-erd.md) — how transactions are persisted.
