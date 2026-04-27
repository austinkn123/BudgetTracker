# Database Schema ERD

This is an Entity Relationship Diagram (ERD) for the current BudgetTracker database schema.

## Entity Relationship Diagram

```mermaid
erDiagram
    USERS {
        int Id PK
        nvarchar_255 Email
        datetime2 CreatedAt
    }

    ACCOUNTS {
        int Id PK
        int UserId FK
        nvarchar_100 Name
        nvarchar_50 AccountType
        datetime2 CreatedAt
    }

    CATEGORIES {
        int Id PK
        int UserId FK
        nvarchar_100 Name
        nvarchar_20 CategoryType
    }

    BUDGETPLANS {
        int Id PK
        int UserId FK
        nvarchar_100 Name
        date PlanMonth
        decimal_18_2 NetIncomeMonthly
        bit IsActive
        datetime2 CreatedAt
        datetime2 UpdatedAt_nullable
    }

    BUDGETPLANENTRIES {
        int Id PK
        int BudgetPlanId FK
        int CategoryId FK_nullable
        nvarchar_20 LineType
        nvarchar_20 Bucket
        nvarchar_20 Cadence
        decimal_18_2 Amount
        decimal_18_2 MonthlyEquivalent
        bit IsStressFactor
        nvarchar_500 Notes_nullable
        int SortOrder
        datetime2 CreatedAt
        datetime2 UpdatedAt_nullable
    }

    TRANSACTIONS {
        int Id PK
        int AccountId FK
        int CategoryId FK_nullable
        nvarchar_20 TransactionType
        decimal_18_2 Amount
        datetime2 OccurredAt
        nvarchar_255 Payee
        nvarchar_1000 Notes
        int TransferAccountId FK_nullable
        datetime2 CreatedAt
    }

    USERS ||--o{ ACCOUNTS : owns
    USERS ||--o{ CATEGORIES : defines
    USERS ||--o{ BUDGETPLANS : owns

    BUDGETPLANS ||--o{ BUDGETPLANENTRIES : contains
    CATEGORIES ||--o{ BUDGETPLANENTRIES : optional_category
    ACCOUNTS ||--o{ TRANSACTIONS : source_account
    CATEGORIES ||--o{ TRANSACTIONS : optional_category
    ACCOUNTS ||--o{ TRANSACTIONS : optional_transfer_target
```

## Key Constraints

- Unique index: ACCOUNTS (UserId, Name)
- Unique index: CATEGORIES (UserId, Name)
- Unique index: BUDGETPLANS (UserId, PlanMonth, Name)
- Index: BUDGETPLANS (UserId, PlanMonth, IsActive) including Name, NetIncomeMonthly
- Index: BUDGETPLANENTRIES (BudgetPlanId, Bucket, LineType) including MonthlyEquivalent, Amount, Cadence, CategoryId, SortOrder
- Check constraint: CATEGORIES.CategoryType in (Expense, Income, Both)
- Check constraint: BUDGETPLANS.NetIncomeMonthly >= 0
- Check constraint: BUDGETPLANENTRIES.LineType in (Income, Expense)
- Check constraint: BUDGETPLANENTRIES.Bucket in (Core, Buffer)
- Check constraint: BUDGETPLANENTRIES.Cadence in (Monthly, Annual)
- Check constraint: BUDGETPLANENTRIES.Amount >= 0
- Check constraint: BUDGETPLANENTRIES.MonthlyEquivalent >= 0
- Check constraint: TRANSACTIONS.TransactionType in (Expense, Income, Transfer, Adjustment)
- Check constraint: TRANSACTIONS.Amount > 0
- Check constraint: transfer rows require TransferAccountId, non-transfer rows require TransferAccountId null
- Budget plan entries cascade on delete from their parent budget plan
- All other foreign keys use delete behavior NO ACTION

## Notes

- The application now uses TRANSACTIONS as the active ledger path.
- BUDGETPLANS and BUDGETPLANENTRIES hold planned cash flow, separate from posted ledger activity.
- The legacy EXPENSES table has been retired from setup and the active domain model.
- The current setup script seeds one baseline budget plan with 13 budget plan entries and a smaller sample ledger of 10 transactions.
- This ERD reflects the current SQL setup script and the latest EF Core model configuration.