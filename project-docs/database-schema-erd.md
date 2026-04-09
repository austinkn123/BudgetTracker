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

    ACCOUNTS ||--o{ TRANSACTIONS : source_account
    CATEGORIES ||--o{ TRANSACTIONS : optional_category
    ACCOUNTS ||--o{ TRANSACTIONS : optional_transfer_target
```

## Key Constraints

- Unique index: ACCOUNTS (UserId, Name)
- Unique index: CATEGORIES (UserId, Name)
- Check constraint: CATEGORIES.CategoryType in (Expense, Income, Both)
- Check constraint: TRANSACTIONS.TransactionType in (Expense, Income, Transfer, Adjustment)
- Check constraint: TRANSACTIONS.Amount > 0
- Check constraint: transfer rows require TransferAccountId, non-transfer rows require TransferAccountId null
- Foreign keys use delete behavior NO ACTION

## Notes

- The application now uses TRANSACTIONS as the active ledger path.
- The legacy EXPENSES table has been retired from setup and the active domain model.
- The baseline EF migration is intentionally empty, so this ERD reflects SQL setup plus EF model configuration.