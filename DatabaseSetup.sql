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

-- Insert sample data
-- Insert a test user
IF NOT EXISTS (SELECT * FROM Users WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT Users ON;
    INSERT INTO Users (Id, Email, CreatedAt) 
    VALUES (1, 'me@budgettracker.com', GETDATE());
    SET IDENTITY_INSERT Users OFF;
    PRINT 'Sample user inserted';
END
GO

-- Insert a default account for the test user
IF NOT EXISTS (SELECT * FROM Accounts WHERE UserId = 1 AND Name = 'Checking')
BEGIN
    INSERT INTO Accounts (UserId, Name, AccountType, CreatedAt)
    VALUES (1, 'Checking', 'Checking', GETDATE());
    PRINT 'Sample account inserted';
END
GO

-- Insert sample categories
IF NOT EXISTS (SELECT * FROM Categories WHERE UserId = 1)
BEGIN
    INSERT INTO Categories (UserId, Name, CategoryType) VALUES 
        (1, 'Groceries', 'Expense'),
        (1, 'Transportation', 'Expense'),
        (1, 'Entertainment', 'Expense'),
        (1, 'Utilities', 'Expense'),
        (1, 'Healthcare', 'Expense'),
        (1, 'Salary', 'Income');
    PRINT 'Sample categories inserted';
END
GO

-- Insert sample income transaction
IF NOT EXISTS (
    SELECT 1
    FROM Transactions t
    INNER JOIN Accounts a ON a.Id = t.AccountId
    WHERE a.UserId = 1
      AND t.TransactionType = 'Income'
)
BEGIN
    DECLARE @IncomeCheckingAccountId INT;
    DECLARE @SalaryCategoryId INT;

    SELECT @IncomeCheckingAccountId = Id FROM Accounts WHERE UserId = 1 AND Name = 'Checking';
    SELECT @SalaryCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Salary';

    INSERT INTO Transactions (AccountId, CategoryId, TransactionType, Amount, OccurredAt, Payee, Notes, CreatedAt)
    VALUES (@IncomeCheckingAccountId, @SalaryCategoryId, 'Income', 4200.00, '2024-12-01', 'Employer', 'Monthly paycheck', '2024-12-01');

    PRINT 'Sample income transaction inserted';
END
GO

-- Insert sample expense transactions
IF NOT EXISTS (
    SELECT 1
    FROM Transactions t
    INNER JOIN Accounts a ON a.Id = t.AccountId
    WHERE a.UserId = 1
      AND t.TransactionType = 'Expense'
)
BEGIN
    DECLARE @ExpenseCheckingAccountId INT;
    DECLARE @GroceriesCategoryId INT;
    DECLARE @TransportationCategoryId INT;
    DECLARE @EntertainmentCategoryId INT;
    DECLARE @UtilitiesCategoryId INT;
    DECLARE @HealthcareCategoryId INT;

    SELECT @ExpenseCheckingAccountId = Id FROM Accounts WHERE UserId = 1 AND Name = 'Checking';
    SELECT @GroceriesCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Groceries';
    SELECT @TransportationCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Transportation';
    SELECT @EntertainmentCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Entertainment';
    SELECT @UtilitiesCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Utilities';
    SELECT @HealthcareCategoryId = Id FROM Categories WHERE UserId = 1 AND Name = 'Healthcare';

    INSERT INTO Transactions (AccountId, CategoryId, TransactionType, Amount, OccurredAt, Payee, Notes, CreatedAt) VALUES 
        (@ExpenseCheckingAccountId, @GroceriesCategoryId, 'Expense', 85.42, '2024-12-01', 'Whole Foods', 'Weekly grocery shopping', '2024-12-01'),
        (@ExpenseCheckingAccountId, @TransportationCategoryId, 'Expense', 45.00, '2024-12-02', 'Uber', 'Ride to office', '2024-12-02'),
        (@ExpenseCheckingAccountId, @EntertainmentCategoryId, 'Expense', 15.99, '2024-12-03', 'Netflix', 'Monthly subscription', '2024-12-03'),
        (@ExpenseCheckingAccountId, @UtilitiesCategoryId, 'Expense', 120.50, '2024-12-04', 'Electric Company', 'November electricity bill', '2024-12-04'),
        (@ExpenseCheckingAccountId, @GroceriesCategoryId, 'Expense', 52.30, '2024-12-05', 'Trader Joe''s', 'Fresh produce', '2024-12-05'),
        (@ExpenseCheckingAccountId, @TransportationCategoryId, 'Expense', 30.00, '2024-12-06', 'Gas Station', 'Fill up the tank', '2024-12-06'),
        (@ExpenseCheckingAccountId, @EntertainmentCategoryId, 'Expense', 12.99, '2024-12-07', 'Spotify', 'Music streaming', '2024-12-07'),
        (@ExpenseCheckingAccountId, @HealthcareCategoryId, 'Expense', 75.00, '2024-12-08', 'Pharmacy', 'Prescription medication', '2024-12-08'),
        (@ExpenseCheckingAccountId, @GroceriesCategoryId, 'Expense', 95.23, '2024-12-09', 'Costco', 'Bulk shopping', '2024-12-09'),
        (@ExpenseCheckingAccountId, @UtilitiesCategoryId, 'Expense', 85.00, '2024-12-10', 'Water Company', 'Monthly water bill', '2024-12-10');

    PRINT 'Sample expense transactions inserted';
END
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
