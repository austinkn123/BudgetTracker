using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.Domain.Migrations
{
    /// <inheritdoc />
    public partial class SyncBudgetPlanningSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Transactions') AND name = 'IX_Transactions_AccountId')
    DROP INDEX IX_Transactions_AccountId ON dbo.Transactions;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Transactions') AND name = 'IX_Transactions_CategoryId')
    DROP INDEX IX_Transactions_CategoryId ON dbo.Transactions;

IF NOT EXISTS (
    SELECT 1 FROM sys.key_constraints
    WHERE [type] = 'UQ'
      AND [name] = 'UQ_Categories_User_Name'
      AND parent_object_id = OBJECT_ID('dbo.Categories')
)
BEGIN
    IF EXISTS (
        SELECT UserId, Name
        FROM dbo.Categories
        GROUP BY UserId, Name
        HAVING COUNT(*) > 1
    )
    BEGIN
        THROW 51001, 'Cannot create UQ_Categories_User_Name because duplicate category names exist per user.', 1;
    END;

    ALTER TABLE dbo.Categories
        ADD CONSTRAINT UQ_Categories_User_Name UNIQUE (UserId, Name);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE [name] = 'CK_Categories_CategoryType'
      AND parent_object_id = OBJECT_ID('dbo.Categories')
)
BEGIN
    ALTER TABLE dbo.Categories WITH CHECK
        ADD CONSTRAINT CK_Categories_CategoryType
        CHECK (CategoryType IN ('Expense', 'Income', 'Both'));
END;

IF OBJECT_ID('dbo.BudgetPlans', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BudgetPlans
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BudgetPlans PRIMARY KEY,
        UserId INT NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        PlanMonth DATE NOT NULL,
        NetIncomeMonthly DECIMAL(18,2) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_BudgetPlans_IsActive DEFAULT (1),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_BudgetPlans_CreatedAt DEFAULT (GETDATE()),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT FK_BudgetPlans_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_BudgetPlans_NetIncome CHECK (NetIncomeMonthly >= 0),
        CONSTRAINT UQ_BudgetPlans_User_Month_Name UNIQUE (UserId, PlanMonth, [Name])
    );
END;

IF OBJECT_ID('dbo.BudgetPlanLines', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BudgetPlanLines
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BudgetPlanLines PRIMARY KEY,
        BudgetPlanId INT NOT NULL,
        CategoryId INT NULL,
        LineType NVARCHAR(20) NOT NULL,
        Bucket NVARCHAR(20) NOT NULL,
        Cadence NVARCHAR(20) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        MonthlyEquivalent DECIMAL(18,2) NOT NULL,
        IsStressFactor BIT NOT NULL CONSTRAINT DF_BudgetPlanLines_IsStressFactor DEFAULT (0),
        Notes NVARCHAR(500) NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_BudgetPlanLines_SortOrder DEFAULT (0),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_BudgetPlanLines_CreatedAt DEFAULT (GETDATE()),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT FK_BudgetPlanLines_BudgetPlans_BudgetPlanId FOREIGN KEY (BudgetPlanId) REFERENCES dbo.BudgetPlans(Id) ON DELETE CASCADE,
        CONSTRAINT FK_BudgetPlanLines_Categories_CategoryId FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_BudgetPlanLines_LineType CHECK (LineType IN ('Income', 'Expense')),
        CONSTRAINT CK_BudgetPlanLines_Bucket CHECK (Bucket IN ('Core', 'Buffer')),
        CONSTRAINT CK_BudgetPlanLines_Cadence CHECK (Cadence IN ('Monthly', 'Annual')),
        CONSTRAINT CK_BudgetPlanLines_Amount CHECK (Amount >= 0),
        CONSTRAINT CK_BudgetPlanLines_MonthlyEq CHECK (MonthlyEquivalent >= 0)
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Transactions')
      AND [name] = 'IX_Transactions_AccountId_OccurredAt'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Transactions_AccountId_OccurredAt
        ON dbo.Transactions (AccountId, OccurredAt)
        INCLUDE (TransactionType, Amount, CategoryId);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Transactions')
      AND [name] = 'IX_Transactions_CategoryId_OccurredAt'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Transactions_CategoryId_OccurredAt
        ON dbo.Transactions (CategoryId, OccurredAt)
        INCLUDE (TransactionType, Amount, AccountId);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.BudgetPlans')
      AND [name] = 'IX_BudgetPlans_User_Month_Active'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_BudgetPlans_User_Month_Active
        ON dbo.BudgetPlans (UserId, PlanMonth, IsActive)
        INCLUDE ([Name], NetIncomeMonthly);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.BudgetPlanLines')
      AND [name] = 'IX_BudgetPlanLines_CategoryId'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_BudgetPlanLines_CategoryId
        ON dbo.BudgetPlanLines (CategoryId);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.BudgetPlanLines')
      AND [name] = 'IX_BudgetPlanLines_Plan_Bucket_Type'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_BudgetPlanLines_Plan_Bucket_Type
        ON dbo.BudgetPlanLines (BudgetPlanId, Bucket, LineType)
        INCLUDE (MonthlyEquivalent, Amount, Cadence, CategoryId, SortOrder);
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
IF OBJECT_ID('dbo.BudgetPlanLines', 'U') IS NOT NULL
    DROP TABLE dbo.BudgetPlanLines;

IF OBJECT_ID('dbo.BudgetPlans', 'U') IS NOT NULL
    DROP TABLE dbo.BudgetPlans;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Transactions') AND name = 'IX_Transactions_AccountId_OccurredAt')
    DROP INDEX IX_Transactions_AccountId_OccurredAt ON dbo.Transactions;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Transactions') AND name = 'IX_Transactions_CategoryId_OccurredAt')
    DROP INDEX IX_Transactions_CategoryId_OccurredAt ON dbo.Transactions;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Transactions') AND name = 'IX_Transactions_AccountId')
    CREATE NONCLUSTERED INDEX IX_Transactions_AccountId ON dbo.Transactions (AccountId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Transactions') AND name = 'IX_Transactions_CategoryId')
    CREATE NONCLUSTERED INDEX IX_Transactions_CategoryId ON dbo.Transactions (CategoryId);
");
        }
    }
}
