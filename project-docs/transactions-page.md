# Transactions Page

A user-facing guide to the **Transactions** experience in BudgetTracker: how to read the ledger, add and edit entries, and how the signed-amount (inflow vs. outflow) convention works.

## Overview

The Transactions page is where you review and manage individual money movements — purchases, income, transfers, and balance corrections. It is rendered by the `TransactionsSection` component (`budgettracker.client/src/features/transactions/`) and is composed of three parts:

- A **month summary** strip (transaction count, active days, and Income / Outflow / Net totals).
- A **calendar** that highlights every day with activity and lets you jump to a specific day.
- A **selected-day ledger** listing each transaction for the chosen date, with running Income, Outflow, and Net totals for that day.

The page works one day at a time: pick a date on the calendar (or click **Today**) and the ledger updates to show just that day's transactions. New entries default to the currently selected day.

Transactions come from two sources:

- **Manual** entries you add through the dialog.
- **Imported** entries synced from your bank via Plaid. Imported rows are marked with an **Imported** chip, may show the account mask (e.g. `•••• 1234`), and can show a **Pending** chip while the bank is still settling them.

## Viewing transactions

### Calendar

The calendar drives the whole page:

- Each day that has at least one transaction is **highlighted**. The highlight color reflects that day's **net** total — green when the day nets positive (more in than out) and red when it nets negative.
- Click any day to load its ledger.
- Changing the month moves the selection to the first of that month, so the month summary follows what you are looking at.
- The **Today** button returns the selection to the current date.

### Month summary

The strip above the calendar summarizes the **visible month** (the month of the selected date):

- A sentence such as *"June 2026 has 12 transactions across 5 active days."*
- **Income** — total of all inflows for the month.
- **Outflow** — total of all outflows for the month.
- **Net** — Income minus Outflow, shown with a leading `+` or `-` and colored green or red.

### Selected-day ledger

For the chosen day you see:

- A header with the full date and a count of transactions.
- Chips for **item count**, **Income**, **Outflow**, and **Net** for that day.
- A list of transactions, each showing:
  - **Payee** (falls back to the category name, then to *"Uncategorized transaction"*).
  - A secondary line combining the **category** and either the **notes** or the **transaction type**.
  - For imported rows: an **Imported** chip, the account mask if available, and a **Pending** chip when applicable.
  - The **amount**, right-aligned, prefixed with `+` (inflow, green) or `-` (outflow, red).

Transactions within a day are sorted newest first. If the day has no transactions, the ledger shows an empty state with an **Add Transaction for This Day** button.

> There is currently no free-text search or column filtering. Navigation is by date through the calendar.

## Adding a transaction

Click **Add Transaction** (top right) or the button in an empty day's ledger to open the dialog. **New manual transactions are recorded as expenses (outflows).**

The dialog has these fields:

| Field | Required | Notes |
|-------|----------|-------|
| **Amount** | Yes | Enter a positive number. The page applies the sign for you (see [Signed amounts](#signed-amounts-inflow-vs-outflow)). Step is `0.01`. |
| **Category** | Yes | Choose from your expense categories. |
| **Date** | Yes | Defaults to the day selected on the calendar. |
| **Payee** | No | Who the money went to / came from. Up to 200 characters. |
| **Notes** | No | Free text, up to 500 characters. |

Click **Add** to save (the button shows *Saving…* while in flight) or **Cancel** to discard. On success you'll see a confirmation message and the ledger refreshes; on failure an error message explains why and the dialog stays open.

### Validation rules

Validation runs on save (React Hook Form + Zod). You must fix any flagged field before the entry saves:

- **Amount** must be **greater than 0**. Enter the magnitude only — never a negative number; the sign is applied automatically.
- **Category** is **required**.
- **Date** is **required**.
- **Payee** must be **200 characters or fewer**.
- **Notes** must be **500 characters or fewer**.

## Editing and deleting a transaction

Click any row in the ledger to open it in the dialog (titled **Edit Transaction**).

### Manual transactions

For a manual **expense**, every field is editable. Edit the values and click **Save**, or use the **Delete** button (bottom left) to remove the transaction. The amount field always shows the **positive magnitude**; the correct sign is re-applied when you save, based on the transaction's type.

> **Current limitation:** editing manual **Income**, **Transfer**, and **Adjustment** transactions is not yet supported. Clicking such a row shows the message *"Editing Income, Transfer, and Adjustment transactions is not yet supported (see BUD-19)."* This guard prevents accidentally losing the transaction's sign while a dedicated type picker is still in development.

### Imported (bank) transactions

Imported rows are **locked**. The dialog shows an info banner — *"Imported from your bank — only category & notes can be edited."* — and:

- **Amount**, **Date**, and **Payee** are read-only.
- Only **Category** and **Notes** can be changed.
- There is **no Delete button** for imported rows; the amount and other source fields are preserved exactly as the bank reported them.

This lets you categorize and annotate bank activity without altering the underlying record.

## Signed amounts (inflow vs. outflow)

BudgetTracker stores every transaction amount as a **signed** value, but the dialog always asks you for a **positive** number. The page translates between the two so you never type a minus sign.

### How the sign is decided

The stored sign depends on the **transaction type**:

| Transaction type | Stored sign | Direction |
|------------------|-------------|-----------|
| **Income** | Positive (`+`) | Inflow |
| **Expense** | Negative (`-`) | Outflow |
| **Transfer** | Negative (`-`) | Outflow |
| **Adjustment** | User-chosen (`+` or `-`) | Either |

When you save, the page takes the magnitude you entered and applies the rule above. When you re-open a transaction to edit it, the page strips the sign and shows the positive magnitude again.

### How direction is displayed

In the ledger and summaries, a transaction counts as an **inflow** if it is **Income**, or an **Adjustment with a positive amount**. Everything else (Expense, Transfer, and negative Adjustments) is an **outflow**.

- Inflows render green with a `+` prefix.
- Outflows render red with a `-` prefix.

### Adjustments

An **Adjustment** is a manual balance correction, and its sign is meaningful:

- A **positive** Adjustment means the balance was understated — it is treated as an **inflow** (green, income side).
- A **negative** Adjustment means the balance was overstated — it is treated as an **outflow** (red, expense side).

Adjustments are the only type that keeps whatever sign was recorded, because the direction is intentionally the user's choice.

### How summary totals are calculated

For each day, and for the visible month, totals are computed as follows:

- **Income total** — the sum of the **magnitudes** of all inflow transactions (always shown as a non-negative number).
- **Outflow total** — the sum of the **magnitudes** of all outflow transactions (always shown as a non-negative number).
- **Net total** — the sum of the **signed** amounts. Because inflows are positive and outflows are negative, Net naturally equals Income minus Outflow, and is shown with a `+` or `-` and colored accordingly.

The month summary is simply the roll-up of every active day in the selected month.

## Gotchas and edge cases

- **Always enter a positive amount.** The minus sign is never typed — it is derived from the transaction type on save. A negative entry is rejected by validation.
- **New manual entries are expenses.** The "Add Transaction" flow records an Expense (outflow). There is not yet a type picker for creating Income, Transfer, or Adjustment entries manually.
- **Some manual edits are blocked.** Income, Transfer, and Adjustment manual transactions can't be edited yet (tracked under BUD-19).
- **Imported rows are mostly read-only.** You can change only category and notes; amount, date, and payee stay as the bank reported them, and they can't be deleted from this dialog.
- **Net color follows the day's net, not the count.** A day with many expenses but a large income deposit can still highlight green on the calendar.
- **Income and Outflow totals are always non-negative.** The direction is conveyed by which bucket a transaction lands in and by the Net figure — not by a sign on those two totals.
- **Day navigation only.** There is no keyword search or column-level filtering; use the calendar to move between dates and months.

## Related documentation

- [Frontend Architecture](frontend-architecture.md) — feature-slice structure these components follow.
- [Database Schema (ERD)](database-schema-erd.md) — how transactions are persisted.
