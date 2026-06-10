using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class TransactionManagerTests
{
    private readonly Mock<ITransactionAccessor> _accessor = new(MockBehavior.Strict);
    private readonly TransactionEngine _engine = new();

    private TransactionManager BuildSut() => new(_engine, _accessor.Object);

    private static Transaction BuildImportedExpense(int id = 1) => new()
    {
        Id = id,
        AccountId = 7,
        CategoryId = 3,
        TransactionType = "Expense",
        Amount = -25m,
        OccurredAt = DateTime.UtcNow.Date.AddDays(-1),
        Payee = "Starbucks",
        Notes = null,
        PlaidTransactionId = "plaid-txn-1",
        IsImported = true
    };

    [Fact]
    public async Task Update_ImportedTransaction_RejectsAmountChange()
    {
        var existing = BuildImportedExpense();
        _accessor.Setup(a => a.GetByIdAsync(existing.Id, 5)).ReturnsAsync(existing);

        var attempted = BuildImportedExpense();
        attempted.Amount = -100m; // tampering with amount

        var result = await BuildSut().UpdateAsync(attempted, userId: 5);

        Assert.False(result.IsSuccess);
        Assert.Equal("Imported transactions are read-only except for Category and Notes", result.Error);
    }

    [Fact]
    public async Task Update_ImportedTransaction_RejectsPayeeChange()
    {
        var existing = BuildImportedExpense();
        _accessor.Setup(a => a.GetByIdAsync(existing.Id, 5)).ReturnsAsync(existing);

        var attempted = BuildImportedExpense();
        attempted.Payee = "Different Merchant";

        var result = await BuildSut().UpdateAsync(attempted, userId: 5);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Update_ImportedTransaction_RejectsDateChange()
    {
        var existing = BuildImportedExpense();
        _accessor.Setup(a => a.GetByIdAsync(existing.Id, 5)).ReturnsAsync(existing);

        var attempted = BuildImportedExpense();
        attempted.OccurredAt = existing.OccurredAt.AddDays(-5);

        var result = await BuildSut().UpdateAsync(attempted, userId: 5);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Update_ImportedTransaction_AllowsCategoryAndNotesChanges()
    {
        var existing = BuildImportedExpense();
        _accessor.Setup(a => a.GetByIdAsync(existing.Id, 5)).ReturnsAsync(existing);
        _accessor.Setup(a => a.AccountBelongsToUserAsync(existing.AccountId, 5)).ReturnsAsync(true);
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Transaction>(), 5)).ReturnsAsync(true);

        var attempted = BuildImportedExpense();
        attempted.CategoryId = 42;
        attempted.Notes = "Reimbursable";

        var result = await BuildSut().UpdateAsync(attempted, userId: 5);

        Assert.True(result.IsSuccess);
    }
}
