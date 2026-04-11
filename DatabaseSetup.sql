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

-- Insert income + monthly expenses + irregular expense buffer (monthly equivalent)
INSERT INTO Transactions (AccountId, CategoryId, TransactionType, Amount, OccurredAt, Payee, Notes, CreatedAt) VALUES
    (@CheckingAccountId, @NetIncomeCategoryId, 'Income', 5646.00, '2025-01-01', 'Net Income', 'Monthly net income', '2025-01-01'),

    (@CheckingAccountId, @MortgageCategoryId, 'Expense', 3208.00, '2025-01-02', 'Mortgage', 'Monthly core expense', '2025-01-02'),
    (@CheckingAccountId, @BillsCategoryId, 'Expense', 425.00, '2025-01-03', 'Utilities + Pest Control', 'Monthly core expense', '2025-01-03'),
    (@CheckingAccountId, @CarInsuranceCategoryId, 'Expense', 391.00, '2025-01-04', 'Car Insurance', 'Monthly core expense', '2025-01-04'),
    (@CheckingAccountId, @FoodCategoryId, 'Expense', 400.00, '2025-01-05', 'Food', 'Monthly core expense', '2025-01-05'),
    (@CheckingAccountId, @GasTollsCategoryId, 'Expense', 50.00, '2025-01-06', 'Gas + Tolls', 'Monthly core expense', '2025-01-06'),
    (@CheckingAccountId, @MiscCategoryId, 'Expense', 68.00, '2025-01-07', 'Subscriptions + Misc', 'Monthly core expense', '2025-01-07'),

    (@CheckingAccountId, @RegInspectionCategoryId, 'Expense', 12.50, '2025-01-08', 'Car registration/inspection', 'Irregular expense monthly equivalent', '2025-01-08'),
    (@CheckingAccountId, @MaintenanceCategoryId, 'Expense', 167.00, '2025-01-09', 'Home maintenance/repairs', 'Irregular expense monthly equivalent', '2025-01-09'),
    (@CheckingAccountId, @CopaysCategoryId, 'Expense', 42.00, '2025-01-10', 'Health/dental co-pays', 'Irregular expense monthly equivalent', '2025-01-10'),
    (@CheckingAccountId, @TravelCategoryId, 'Expense', 100.00, '2025-01-11', 'Travel / Gifts / Holidays', 'Irregular expense monthly equivalent', '2025-01-11'),
    (@CheckingAccountId, @GasIncreaseCategoryId, 'Expense', 50.00, '2025-01-12', 'Gas/Tolls increase', 'Irregular expense monthly equivalent', '2025-01-12'),
    (@CheckingAccountId, @EmergencyCategoryId, 'Expense', 42.00, '2025-01-13', 'Unexpected emergencies', 'Irregular expense monthly equivalent', '2025-01-13');
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
SELECT 'Transactions', COUNT(*) FROM Transactions;
GO
