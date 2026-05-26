using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Tests.Engines;

public class TransactionEngineTests
{
    private readonly TransactionEngine _sut = new();

    private static Transaction BuildValidExpense(decimal amount = -10m) => new()
    {
        AccountId = 1,
        CategoryId = 1,
        TransactionType = "Expense",
        Amount = amount,
        OccurredAt = DateTime.UtcNow.AddDays(-1),
    };

    private static Transaction BuildValidIncome(decimal amount = 100m) => new()
    {
        AccountId = 1,
        CategoryId = 1,
        TransactionType = "Income",
        Amount = amount,
        OccurredAt = DateTime.UtcNow.AddDays(-1),
    };

    // ── Happy paths ─────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ExpenseWithNegativeAmount_Succeeds()
    {
        var result = _sut.ValidateTransaction(BuildValidExpense(-25m));
        Assert.Null(result);
    }

    [Fact]
    public void Validate_IncomeWithPositiveAmount_Succeeds()
    {
        var result = _sut.ValidateTransaction(BuildValidIncome(500m));
        Assert.Null(result);
    }

    [Fact]
    public void Validate_AdjustmentWithPositiveAmount_Succeeds()
    {
        var txn = BuildValidIncome();
        txn.TransactionType = "Adjustment";
        txn.Amount = 15m;
        var result = _sut.ValidateTransaction(txn);
        Assert.Null(result);
    }

    [Fact]
    public void Validate_AdjustmentWithNegativeAmount_Succeeds()
    {
        var txn = BuildValidIncome();
        txn.TransactionType = "Adjustment";
        txn.Amount = -15m;
        var result = _sut.ValidateTransaction(txn);
        Assert.Null(result);
    }

    [Fact]
    public void Validate_TransferWithNegativeAmount_Succeeds()
    {
        var txn = BuildValidExpense();
        txn.TransactionType = "Transfer";
        txn.Amount = -50m;
        txn.TransferAccountId = 2;
        var result = _sut.ValidateTransaction(txn);
        Assert.Null(result);
    }

    // ── New sign-vs-type rules ──────────────────────────────────────────────

    [Fact]
    public void Validate_ExpenseWithPositiveAmount_ReturnsError()
    {
        var txn = BuildValidExpense(50m);
        var result = _sut.ValidateTransaction(txn);
        Assert.Equal("Expense amount must be negative", result);
    }

    [Fact]
    public void Validate_IncomeWithNegativeAmount_ReturnsError()
    {
        var txn = BuildValidIncome(-50m);
        var result = _sut.ValidateTransaction(txn);
        Assert.Equal("Income amount must be positive", result);
    }

    [Fact]
    public void Validate_TransferWithPositiveAmount_ReturnsError()
    {
        var txn = BuildValidExpense();
        txn.TransactionType = "Transfer";
        txn.Amount = 50m;
        txn.TransferAccountId = 2;
        var result = _sut.ValidateTransaction(txn);
        Assert.Equal("Transfer source amount must be negative", result);
    }

    // ── Zero-amount rule (existing rule, message updated) ───────────────────

    [Fact]
    public void Validate_AmountZero_ReturnsError()
    {
        var txn = BuildValidExpense(0m);
        var result = _sut.ValidateTransaction(txn);
        Assert.Equal("Amount cannot be zero", result);
    }

    // ── Pre-existing rules (regression coverage) ────────────────────────────

    [Fact]
    public void Validate_InvalidAccountId_ReturnsError()
    {
        var txn = BuildValidExpense();
        txn.AccountId = 0;
        var result = _sut.ValidateTransaction(txn);
        Assert.Equal("A valid account is required", result);
    }

    [Fact]
    public void Validate_UnknownTransactionType_ReturnsError()
    {
        var txn = BuildValidExpense();
        txn.TransactionType = "Bogus";
        var result = _sut.ValidateTransaction(txn);
        Assert.Equal(
            "Transaction type must be one of: Expense, Income, Transfer, Adjustment",
            result);
    }

    [Fact]
    public void Validate_FutureOccurredAt_ReturnsError()
    {
        var txn = BuildValidExpense();
        txn.OccurredAt = DateTime.UtcNow.AddDays(7);
        var result = _sut.ValidateTransaction(txn);
        Assert.Equal("Transaction date cannot be in the future", result);
    }

    [Fact]
    public void Validate_NonPositiveCategoryId_ReturnsError()
    {
        var txn = BuildValidExpense();
        txn.CategoryId = 0;
        var result = _sut.ValidateTransaction(txn);
        Assert.Equal("Category ID must be greater than zero when provided", result);
    }

    [Fact]
    public void Validate_NullCategoryId_Succeeds()
    {
        var txn = BuildValidExpense();
        txn.CategoryId = null;
        var result = _sut.ValidateTransaction(txn);
        Assert.Null(result);
    }

    [Fact]
    public void Validate_TransferWithoutDestination_ReturnsError()
    {
        var txn = BuildValidExpense();
        txn.TransactionType = "Transfer";
        txn.Amount = -50m;
        txn.TransferAccountId = null;
        var result = _sut.ValidateTransaction(txn);
        Assert.Equal("Transfer transactions require a destination account", result);
    }

    [Fact]
    public void Validate_NonTransferWithDestination_ReturnsError()
    {
        var txn = BuildValidExpense();
        txn.TransferAccountId = 2;
        var result = _sut.ValidateTransaction(txn);
        Assert.Equal("Only transfer transactions can specify a destination account", result);
    }
}
