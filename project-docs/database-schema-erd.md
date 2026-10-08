# Database Schema ERD

This is an Entity Relationship Diagram (ERD) for the current BudgetTracker database schema.

## Data ownership

The database stores **budget data only**. Two external systems own their own data and are
referenced here by the minimum linkage needed:

- **AWS Cognito owns identity.** `USERS` holds nothing but `CognitoSub` — the subject claim
  used to key budget rows to a login. The email address, and every other profile attribute,
  is read live from the JWT and projected onto `GET /api/users/me` at request time. It is
  deliberately not persisted, so it can never go stale.
- **Plaid owns institution data.** `PLAIDITEMS` and `PLAIDACCOUNTS` exist because Plaid cannot
  hold them for us: the encrypted `access_token`, the `/transactions/sync` cursor, and the
  last-4 mask used for display. Transactions themselves are materialized into `TRANSACTIONS`
  so the app reads its own ledger rather than calling Plaid on every page load.

## Entity Relationship Diagram

```mermaid
erDiagram
    USERS {
        int Id PK
        varchar_255 CognitoSub_nullable
        timestamp CreatedAt
    }

    ACCOUNTS {
        int Id PK
        int UserId FK
        varchar_100 Name
        varchar_50 AccountType
        timestamp CreatedAt
    }

    CATEGORIES {
        int Id PK
        int UserId FK
        varchar_100 Name
        varchar_20 CategoryType
    }

    BUDGETPLANS {
        int Id PK
        int UserId FK
        varchar_100 Name
        date PlanMonth
        numeric_18_2 NetIncomeMonthly
        boolean IsActive
        timestamp CreatedAt
        timestamp UpdatedAt_nullable
    }

    BUDGETPLANENTRIES {
        int Id PK
        int BudgetPlanId FK
        int CategoryId FK_nullable
        varchar_20 LineType
        varchar_20 Bucket
        varchar_20 Cadence
        numeric_18_2 Amount
        numeric_18_2 MonthlyEquivalent
        boolean IsStressFactor
        varchar_500 Notes_nullable
        int SortOrder
        timestamp CreatedAt
        timestamp UpdatedAt_nullable
    }

    TRANSACTIONS {
        int Id PK
        int AccountId FK
        int CategoryId FK_nullable
        varchar_20 TransactionType
        numeric_18_2 Amount
        timestamp OccurredAt
        varchar_255 Payee_nullable
        varchar_1000 Notes_nullable
        int TransferAccountId FK_nullable
        varchar_100 PlaidTransactionId_nullable
        varchar_100 PlaidAccountId_nullable
        boolean IsImported
        boolean IsPending
        timestamp CreatedAt
    }

    PLAIDITEMS {
        int Id PK
        int UserId FK
        varchar_100 PlaidItemId
        varchar_50 InstitutionId
        varchar_200 InstitutionName
        varchar_500 AccessTokenEncrypted
        varchar_500 SyncCursor_nullable
        boolean IsActive
        timestamp ConsentExpiresAt_nullable
        timestamp LastSyncedAt_nullable
        timestamp CreatedAt
    }

    PLAIDACCOUNTS {
        int Id PK
        int PlaidItemId FK
        varchar_100 PlaidAccountId
        varchar_4 Mask_nullable
        varchar_200 Name
        varchar_50 AccountType
        varchar_50 AccountSubtype_nullable
    }

    USERS ||--o{ ACCOUNTS : owns
    USERS ||--o{ CATEGORIES : defines
    USERS ||--o{ BUDGETPLANS : owns
    USERS ||--o| PLAIDITEMS : links

    BUDGETPLANS ||--o{ BUDGETPLANENTRIES : contains
    CATEGORIES ||--o{ BUDGETPLANENTRIES : optional_category
    ACCOUNTS ||--o{ TRANSACTIONS : source_account
    CATEGORIES ||--o{ TRANSACTIONS : optional_category
    ACCOUNTS ||--o{ TRANSACTIONS : optional_transfer_target
    PLAIDITEMS ||--o{ PLAIDACCOUNTS : holds
```

## Key Constraints

- Unique index: ACCOUNTS (UserId, Name)
- Unique index: CATEGORIES (UserId, Name)
- Unique index: BUDGETPLANS (UserId, PlanMonth, Name)
- Unique filtered index: USERS (CognitoSub) where CognitoSub is not null
- Unique filtered index: TRANSACTIONS (PlaidTransactionId) where PlaidTransactionId is not null — the Plaid dedup key
- Unique index: PLAIDITEMS (PlaidItemId)
- Index: PLAIDITEMS (UserId) — non-unique; a user may link many institutions (one active row per linked bank)
- Unique index: PLAIDACCOUNTS (PlaidAccountId)
- Index: BUDGETPLANS (UserId, PlanMonth, IsActive) including Name, NetIncomeMonthly
- Index: BUDGETPLANENTRIES (BudgetPlanId, Bucket, LineType) including MonthlyEquivalent, Amount, Cadence, CategoryId, SortOrder
- Index: TRANSACTIONS (AccountId, OccurredAt) including TransactionType, Amount, CategoryId
- Index: TRANSACTIONS (CategoryId, OccurredAt) including TransactionType, Amount, AccountId
- Check constraint: CATEGORIES.CategoryType in (Expense, Income, Both)
- Check constraint: BUDGETPLANS.NetIncomeMonthly >= 0
- Check constraint: BUDGETPLANENTRIES.LineType in (Income, Expense)
- Check constraint: BUDGETPLANENTRIES.Bucket in (Core, Buffer)
- Check constraint: BUDGETPLANENTRIES.Cadence in (Monthly, Annual)
- Check constraint: BUDGETPLANENTRIES.Amount >= 0
- Check constraint: BUDGETPLANENTRIES.MonthlyEquivalent >= 0
- Check constraint: TRANSACTIONS.TransactionType in (Expense, Income, Transfer, Adjustment)
- Check constraint: TRANSACTIONS.Amount <> 0 — amounts are signed (BUD-18); expenses and transfers are negative
- Check constraint: transfer rows require TransferAccountId, non-transfer rows require TransferAccountId null
- Budget plan entries cascade on delete from their parent budget plan
- Plaid accounts cascade on delete from their parent Plaid item
- All other foreign keys use delete behavior NO ACTION

## Notes

- **Bootstrap a database with `dotnet ef database update`** — EF migrations are the single
  source of schema truth. The old hand-maintained `DatabaseSetup.sql` was retired because it
  had drifted badly (no Cognito column, no Plaid tables, a stale `Amount > 0` constraint) and
  it hard-deleted the seed profile's data on every run.
- The application uses TRANSACTIONS as the active ledger path. Rows arrive either by manual
  entry or by Plaid sync; `IsImported` marks the latter and locks their merchant, amount and
  date against edits.
- BUDGETPLANS and BUDGETPLANENTRIES hold planned cash flow, separate from posted ledger activity.
- `TRANSACTIONS.PlaidAccountId` is Plaid's raw external account id, not a foreign key to
  `PLAIDACCOUNTS.Id`; the join is by that external id.
- ACCOUNTS rows are created automatically by `PlaidManager` during token exchange — there is no
  accounts endpoint. An account is the FK anchor a transaction hangs off.
- The legacy EXPENSES table has been retired from setup and the active domain model.
- The database ships with no seed data.
- This ERD reflects the EF Core model as of migration `DropUserEmail`.
