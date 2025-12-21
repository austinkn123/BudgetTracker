-- Budget Tracker Database Setup Script
-- This script creates the necessary tables and sample data for the Budget Tracker application

USE BudgetTracker;
GO

-- Create Users table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
BEGIN
    CREATE TABLE Users (
        Id INT PRIMARY KEY IDENTITY(1,1),
        CognitoUserId NVARCHAR(255) NOT NULL,
        Email NVARCHAR(255) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
    );
    PRINT 'Users table created successfully';
END
GO

-- Create Categories table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Categories' AND xtype='U')
BEGIN
    CREATE TABLE Categories (
        Id INT PRIMARY KEY IDENTITY(1,1),
        UserId INT NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        CONSTRAINT FK_Categories_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
    );
    PRINT 'Categories table created successfully';
END
GO

-- Create Expenses table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Expenses' AND xtype='U')
BEGIN
    CREATE TABLE Expenses (
        Id INT PRIMARY KEY IDENTITY(1,1),
        UserId INT NOT NULL,
        CategoryId INT NOT NULL,
        Amount DECIMAL(18, 2) NOT NULL,
        Date DATETIME2 NOT NULL,
        Merchant NVARCHAR(255),
        Notes NVARCHAR(1000),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Expenses_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Expenses_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE NO ACTION
    );
    PRINT 'Expenses table created successfully';
END
GO

-- Insert sample data
-- Insert a test user
IF NOT EXISTS (SELECT * FROM Users WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT Users ON;
    INSERT INTO Users (Id, CognitoUserId, Email, CreatedAt) 
    VALUES (1, 'test-cognito-123', 'test@budgettracker.com', GETDATE());
    SET IDENTITY_INSERT Users OFF;
    PRINT 'Sample user inserted';
END
GO

-- Insert sample categories
IF NOT EXISTS (SELECT * FROM Categories WHERE UserId = 1)
BEGIN
    INSERT INTO Categories (UserId, Name) VALUES 
        (1, 'Groceries'),
        (1, 'Transportation'),
        (1, 'Entertainment'),
        (1, 'Utilities'),
        (1, 'Healthcare');
    PRINT 'Sample categories inserted';
END
GO

-- Insert sample expenses
IF NOT EXISTS (SELECT * FROM Expenses WHERE UserId = 1)
BEGIN
    INSERT INTO Expenses (UserId, CategoryId, Amount, Date, Merchant, Notes, CreatedAt) VALUES 
        (1, 1, 85.42, '2024-12-01', 'Whole Foods', 'Weekly grocery shopping', '2024-12-01'),
        (1, 2, 45.00, '2024-12-02', 'Uber', 'Ride to office', '2024-12-02'),
        (1, 3, 15.99, '2024-12-03', 'Netflix', 'Monthly subscription', '2024-12-03'),
        (1, 4, 120.50, '2024-12-04', 'Electric Company', 'November electricity bill', '2024-12-04'),
        (1, 1, 52.30, '2024-12-05', 'Trader Joe''s', 'Fresh produce', '2024-12-05'),
        (1, 2, 30.00, '2024-12-06', 'Gas Station', 'Fill up the tank', '2024-12-06'),
        (1, 3, 12.99, '2024-12-07', 'Spotify', 'Music streaming', '2024-12-07'),
        (1, 5, 75.00, '2024-12-08', 'Pharmacy', 'Prescription medication', '2024-12-08'),
        (1, 1, 95.23, '2024-12-09', 'Costco', 'Bulk shopping', '2024-12-09'),
        (1, 4, 85.00, '2024-12-10', 'Water Company', 'Monthly water bill', '2024-12-10');
    PRINT 'Sample expenses inserted';
END
GO

PRINT 'Database setup completed successfully!';
GO

-- Display summary
SELECT 'Users' as TableName, COUNT(*) as RecordCount FROM Users
UNION ALL
SELECT 'Categories', COUNT(*) FROM Categories
UNION ALL
SELECT 'Expenses', COUNT(*) FROM Expenses;
GO
