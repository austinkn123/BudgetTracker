-- Budget Tracker Database Setup Script
-- This script creates the necessary tables and sample data for the Budget Tracker application

USE BudgetTracker;
GO

-- Create Users table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
BEGIN
    CREATE TABLE Users (
        Id INT PRIMARY KEY IDENTITY(1,1),
        Email NVARCHAR(255) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Users table created successfully';
END
GO

-- Create Accounts table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Accounts' AND xtype='U')
BEGIN
    CREATE TABLE Accounts (
        Id INT PRIMARY KEY IDENTITY(1,1),
        UserId INT NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        AccountType NVARCHAR(50) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Accounts_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION,
        CONSTRAINT UQ_Accounts_User_Name UNIQUE (UserId, Name)
    );
    PRINT 'Accounts table created successfully';
END
GO

-- Create Categories table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Categories' AND xtype='U')
BEGIN
    CREATE TABLE Categories (
        Id INT PRIMARY KEY IDENTITY(1,1),
        UserId INT NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        CategoryType NVARCHAR(20) NOT NULL DEFAULT 'Expense',
        CONSTRAINT FK_Categories_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_Categories_CategoryType CHECK (CategoryType IN ('Expense', 'Income', 'Both')),
        CONSTRAINT UQ_Categories_User_Name UNIQUE (UserId, Name)
    );
    PRINT 'Categories table created successfully';
END
GO

-- Add CategoryType to existing Categories table if it was created previously
IF EXISTS (SELECT * FROM sysobjects WHERE name='Categories' AND xtype='U')
    AND COL_LENGTH('Categories', 'CategoryType') IS NULL
BEGIN
    ALTER TABLE Categories ADD CategoryType NVARCHAR(20) NOT NULL CONSTRAINT DF_Categories_CategoryType DEFAULT 'Expense';
    PRINT 'CategoryType column added to Categories';
END
GO

-- Create Transactions table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Transactions' AND xtype='U')
BEGIN
    CREATE TABLE Transactions (
        Id INT PRIMARY KEY IDENTITY(1,1),
        AccountId INT NOT NULL,
        CategoryId INT NULL,
        TransactionType NVARCHAR(20) NOT NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        OccurredAt DATETIME2 NOT NULL,
        Payee NVARCHAR(255),
        Notes NVARCHAR(1000),
        TransferAccountId INT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Transactions_Accounts FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Transactions_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Transactions_TransferAccount FOREIGN KEY (TransferAccountId) REFERENCES Accounts(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_Transactions_TransactionType CHECK (TransactionType IN ('Expense', 'Income', 'Transfer', 'Adjustment')),
        CONSTRAINT CK_Transactions_PositiveAmount CHECK (Amount > 0),
        CONSTRAINT CK_Transactions_TransferAccount CHECK (
            (TransactionType = 'Transfer' AND TransferAccountId IS NOT NULL)
            OR (TransactionType <> 'Transfer' AND TransferAccountId IS NULL)
        )
    );
    PRINT 'Transactions table created successfully';
END
GO

-- Create BudgetPlans table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='BudgetPlans' AND xtype='U')
BEGIN
    CREATE TABLE BudgetPlans (
        Id INT PRIMARY KEY IDENTITY(1,1),
        UserId INT NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        PlanMonth DATE NOT NULL,
        NetIncomeMonthly DECIMAL(18, 2) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_BudgetPlans_IsActive DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_BudgetPlans_CreatedAt DEFAULT GETDATE(),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT FK_BudgetPlans_Users_UserId FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_BudgetPlans_NetIncome CHECK (NetIncomeMonthly >= 0),
        CONSTRAINT UQ_BudgetPlans_User_Month_Name UNIQUE (UserId, PlanMonth, Name)
    );
    PRINT 'BudgetPlans table created successfully';
END
GO

-- Create BudgetPlanEntries table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='BudgetPlanEntries' AND xtype='U')
BEGIN
    CREATE TABLE BudgetPlanEntries (
        Id INT PRIMARY KEY IDENTITY(1,1),
        BudgetPlanId INT NOT NULL,
        CategoryId INT NULL,
        LineType NVARCHAR(20) NOT NULL,
        Bucket NVARCHAR(20) NOT NULL,
        Cadence NVARCHAR(20) NOT NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        MonthlyEquivalent DECIMAL(18, 2) NOT NULL,
        IsStressFactor BIT NOT NULL CONSTRAINT DF_BudgetPlanEntries_IsStressFactor DEFAULT 0,
        Notes NVARCHAR(500) NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_BudgetPlanEntries_SortOrder DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_BudgetPlanEntries_CreatedAt DEFAULT GETDATE(),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT FK_BudgetPlanEntries_BudgetPlans_BudgetPlanId FOREIGN KEY (BudgetPlanId) REFERENCES BudgetPlans(Id) ON DELETE CASCADE,
        CONSTRAINT FK_BudgetPlanEntries_Categories_CategoryId FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_BudgetPlanEntries_LineType CHECK (LineType IN ('Income', 'Expense')),
        CONSTRAINT CK_BudgetPlanEntries_Bucket CHECK (Bucket IN ('Core', 'Buffer')),
        CONSTRAINT CK_BudgetPlanEntries_Cadence CHECK (Cadence IN ('Monthly', 'Annual')),
        CONSTRAINT CK_BudgetPlanEntries_Amount CHECK (Amount >= 0),
        CONSTRAINT CK_BudgetPlanEntries_MonthlyEq CHECK (MonthlyEquivalent >= 0)
    );
    PRINT 'BudgetPlanEntries table created successfully';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.BudgetPlans')
      AND [name] = 'IX_BudgetPlans_User_Month_Active'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_BudgetPlans_User_Month_Active
        ON BudgetPlans (UserId, PlanMonth, IsActive)
        INCLUDE (Name, NetIncomeMonthly);
    PRINT 'IX_BudgetPlans_User_Month_Active created successfully';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.BudgetPlanEntries')
      AND [name] = 'IX_BudgetPlanEntries_CategoryId'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_BudgetPlanEntries_CategoryId
        ON BudgetPlanEntries (CategoryId);
    PRINT 'IX_BudgetPlanEntries_CategoryId created successfully';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.BudgetPlanEntries')
      AND [name] = 'IX_BudgetPlanEntries_Plan_Bucket_Type'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_BudgetPlanEntries_Plan_Bucket_Type
        ON BudgetPlanEntries (BudgetPlanId, Bucket, LineType)
        INCLUDE (MonthlyEquivalent, Amount, Cadence, CategoryId, SortOrder);
    PRINT 'IX_BudgetPlanEntries_Plan_Bucket_Type created successfully';
END
GO

-- Drop legacy Expenses table if it exists.
IF EXISTS (SELECT * FROM sysobjects WHERE name='Expenses' AND xtype='U')
BEGIN
    DROP TABLE Expenses;
    PRINT 'Legacy Expenses table dropped';
END
GO

-- Insert budget data
-- Keep user Id 1 as the local seeded profile.
IF NOT EXISTS (SELECT * FROM Users WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT Users ON;
    INSERT INTO Users (Id, Email, CreatedAt) 
    VALUES (1, 'me@budgettracker.com', GETDATE());
    SET IDENTITY_INSERT Users OFF;
    PRINT 'Sample user inserted';
END
GO

-- Insert a default account for the seeded user
IF NOT EXISTS (SELECT * FROM Accounts WHERE UserId = 1 AND Name = 'Checking')
BEGIN
    INSERT INTO Accounts (UserId, Name, AccountType, CreatedAt)
    VALUES (1, 'Checking', 'Checking', GETDATE());
    PRINT 'Seed account inserted';
END
GO

-- Reseed this profile on each run so data reflects this budget exactly.
DELETE t
FROM Transactions t
INNER JOIN Accounts a ON a.Id = t.AccountId
WHERE a.UserId = 1;

DELETE FROM BudgetPlans
WHERE UserId = 1;

DELETE FROM Categories
WHERE UserId = 1;
GO

-- Insert budget categories
INSERT INTO Categories (UserId, Name, CategoryType) VALUES
    (1, 'Net Income', 'Income'),
    (1, 'Mortgage', 'Expense'),
    (1, 'Bills / Utilities / pest control', 'Expense'),
    (1, 'Car Insurance', 'Expense'),
    (1, 'Food (Groceries + Eating Out)', 'Expense'),
    (1, 'Gas + Tolls', 'Expense'),
    (1, 'Miscellaneous (GitHub Copilot, Hims, Spotify)', 'Expense'),
    (1, 'Car registration/inspection', 'Expense'),
    (1, 'Home maintenance/repairs', 'Expense'),
    (1, 'Health/dental co-pays', 'Expense'),
    (1, 'Travel / Gifts / Holidays', 'Expense'),
    (1, 'Gas/Tolls increase (visiting friends)', 'Expense'),
    (1, 'Unexpected emergencies', 'Expense');
GO

DECLARE @CheckingAccountId INT;
DECLARE @NetIncomeCategoryId INT;
DECLARE @MortgageCategoryId INT;
DECLARE @BillsCategoryId INT;
DECLARE @CarInsuranceCategoryId INT;
DECLARE @FoodCategoryId INT;
DECLARE @GasTollsCategoryId INT;
DECLARE @MiscCategoryId INT;
DECLARE @RegInspectionCategoryId INT;
DECLARE @MaintenanceCategoryId INT;
DECLARE @CopaysCategoryId INT;
DECLARE @TravelCategoryId INT;
DECLARE @GasIncreaseCategoryId INT;
DECLARE @EmergencyCategoryId INT;
DECLARE @BudgetPlanId INT;

SELECT @CheckingAccountId = Id FROM Accounts WHERE UserId = 1 AND Name = 'Checking';

SELECT @NetIncomeCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Net Income';
SELECT @MortgageCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Mortgage';
SELECT @BillsCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Bills / Utilities / pest control';
SELECT @CarInsuranceCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Car Insurance';
SELECT @FoodCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Food (Groceries + Eating Out)';
SELECT @GasTollsCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Gas + Tolls';
SELECT @MiscCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Miscellaneous (GitHub Copilot, Hims, Spotify)';
SELECT @RegInspectionCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Car registration/inspection';
SELECT @MaintenanceCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Home maintenance/repairs';
SELECT @CopaysCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Health/dental co-pays';
SELECT @TravelCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Travel / Gifts / Holidays';
SELECT @GasIncreaseCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Gas/Tolls increase (visiting friends)';
SELECT @EmergencyCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Unexpected emergencies';

INSERT INTO BudgetPlans (UserId, Name, PlanMonth, NetIncomeMonthly, IsActive)
VALUES (1, 'Baseline Budget', '2026-04-01', 5646.00, 1);

SET @BudgetPlanId = SCOPE_IDENTITY();

INSERT INTO BudgetPlanEntries (BudgetPlanId, CategoryId, LineType, Bucket, Cadence, Amount, MonthlyEquivalent, SortOrder) VALUES
    (@BudgetPlanId, @NetIncomeCategoryId, 'Income', 'Core', 'Monthly', 5646.00, 5646.00, 10),
    (@BudgetPlanId, @MortgageCategoryId, 'Expense', 'Core', 'Monthly', 3208.00, 3208.00, 20),
    (@BudgetPlanId, @BillsCategoryId, 'Expense', 'Core', 'Monthly', 425.00, 425.00, 30),
    (@BudgetPlanId, @CarInsuranceCategoryId, 'Expense', 'Core', 'Monthly', 391.00, 391.00, 40),
    (@BudgetPlanId, @FoodCategoryId, 'Expense', 'Core', 'Monthly', 400.00, 400.00, 50),
    (@BudgetPlanId, @GasTollsCategoryId, 'Expense', 'Core', 'Monthly', 50.00, 50.00, 60),
    (@BudgetPlanId, @MiscCategoryId, 'Expense', 'Core', 'Monthly', 68.00, 68.00, 70),
    (@BudgetPlanId, @RegInspectionCategoryId, 'Expense', 'Buffer', 'Annual', 150.00, 12.50, 80),
    (@BudgetPlanId, @MaintenanceCategoryId, 'Expense', 'Buffer', 'Annual', 2004.00, 167.00, 90),
    (@BudgetPlanId, @CopaysCategoryId, 'Expense', 'Buffer', 'Annual', 504.00, 42.00, 100),
    (@BudgetPlanId, @TravelCategoryId, 'Expense', 'Buffer', 'Annual', 1200.00, 100.00, 110),
    (@BudgetPlanId, @GasIncreaseCategoryId, 'Expense', 'Buffer', 'Annual', 600.00, 50.00, 120),
    (@BudgetPlanId, @EmergencyCategoryId, 'Expense', 'Buffer', 'Annual', 504.00, 42.00, 130);

-- Insert sample April 2026 ledger activity without mirroring the full budget plan.
INSERT INTO Transactions (AccountId, CategoryId, TransactionType, Amount, OccurredAt, Payee, Notes, CreatedAt) VALUES
    (@CheckingAccountId, @NetIncomeCategoryId, 'Income', 2823.00, '2026-04-01', 'Employer Direct Deposit', 'Semi-monthly paycheck', '2026-04-01'),
    (@CheckingAccountId, @MortgageCategoryId, 'Expense', 3208.00, '2026-04-02', 'First National Mortgage', 'April mortgage payment', '2026-04-02'),
    (@CheckingAccountId, @BillsCategoryId, 'Expense', 187.45, '2026-04-05', 'City Utilities', 'Water and electric bill', '2026-04-05'),
    (@CheckingAccountId, @FoodCategoryId, 'Expense', 96.32, '2026-04-07', 'H-E-B', 'Weekly groceries', '2026-04-07'),
    (@CheckingAccountId, @GasTollsCategoryId, 'Expense', 43.18, '2026-04-09', 'Shell', 'Fuel for commuting', '2026-04-09'),
    (@CheckingAccountId, @CarInsuranceCategoryId, 'Expense', 391.00, '2026-04-12', 'GEICO', 'Monthly policy payment', '2026-04-12'),
    (@CheckingAccountId, @MiscCategoryId, 'Expense', 10.00, '2026-04-14', 'GitHub', 'Copilot subscription', '2026-04-14'),
    (@CheckingAccountId, @NetIncomeCategoryId, 'Income', 2823.00, '2026-04-15', 'Employer Direct Deposit', 'Semi-monthly paycheck', '2026-04-15'),
    (@CheckingAccountId, @FoodCategoryId, 'Expense', 28.64, '2026-04-18', 'Chipotle', 'Dinner with friends', '2026-04-18'),
    (@CheckingAccountId, @TravelCategoryId, 'Expense', 146.20, '2026-04-23', 'Southwest Airlines', 'Booked spring travel', '2026-04-23');
GO

PRINT 'Budget profile seed data inserted';
GO

PRINT 'Database setup completed successfully!';
GO

-- Display summary
SELECT 'Users' as TableName, COUNT(*) as RecordCount FROM Users
UNION ALL
SELECT 'Accounts', COUNT(*) FROM Accounts
UNION ALL
SELECT 'Categories', COUNT(*) FROM Categories
UNION ALL
SELECT 'BudgetPlans', COUNT(*) FROM BudgetPlans
UNION ALL
SELECT 'BudgetPlanEntries', COUNT(*) FROM BudgetPlanEntries
UNION ALL
SELECT 'Transactions', COUNT(*) FROM Transactions;
GO
