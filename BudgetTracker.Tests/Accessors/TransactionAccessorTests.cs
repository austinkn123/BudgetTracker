using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Tests.Accessors;

/// <summary>
/// <see cref="TransactionAccessor"/> against the in-memory provider. Only paths that avoid ILIKE and ExecuteUpdate
/// (which the provider cannot run) are covered here.
/// </summary>
public class TransactionAccessorTests : IDisposable
{
    private const int UserId = 5;
    private readonly BudgetTrackerDbContext _context;

    public TransactionAccessorTests()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new BudgetTrackerDbContext(options);
        _context.Accounts.AddRange(
            new Account { Id = 1, UserId = UserId, Name = "Checking", AccountType = "depository" },
            new Account { Id = 2, UserId = UserId, Name = "Savings", AccountType = "depository" },
            new Account { Id = 3, UserId = 6, Name = "Someone else", AccountType = "depository" });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private TransactionAccessor BuildSut() => new(_context);

    private static Transaction Row(
        int id, int accountId = 1, string type = "Expense", decimal amount = -10m, string? plaidId = null,
        string? plaidAccountId = "chk", string? plaidCategory = "GENERAL_MERCHANDISE", int? categoryId = null,
        bool pending = false, int? transferAccountId = null) => new()
    {
        Id = id,
        AccountId = accountId,
        TransactionType = type,
        Amount = amount,
        OccurredAt = new DateTime(2026, 10, 1),
        PlaidTransactionId = plaidId,
        PlaidAccountId = plaidAccountId,
        PlaidCategoryPrimary = plaidCategory,
        CategoryId = categoryId,
        IsPending = pending,
        IsImported = plaidId is not null,
        TransferAccountId = transferAccountId
    };

    private async Task SeedAsync(params Transaction[] rows)
    {
        _context.Transactions.AddRange(rows);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private Task<Transaction> ReloadAsync(int id) => _context.Transactions.AsNoTracking().SingleAsync(t => t.Id == id);

    [Fact]
    public async Task GetLatestCategoryByPayeeAsync_ReturnsTheNewestPickPerPayee_ForThisUserOnly()
    {
        var older = Row(1, categoryId: 10);
        older.Payee = "Amazon";
        var newer = Row(2, categoryId: 20);
        newer.Payee = "amazon";
        newer.OccurredAt = older.OccurredAt.AddDays(1);
        var uncategorized = Row(3);
        uncategorized.Payee = "Target";
        var someoneElses = Row(4, accountId: 3, categoryId: 30);
        someoneElses.Payee = "Walmart";
        await SeedAsync(older, newer, uncategorized, someoneElses);

        var result = await BuildSut().GetLatestCategoryByPayeeAsync(UserId);

        Assert.Equal(20, Assert.Single(result).Value);
        Assert.Equal(20, result["AMAZON"]);
    }

    // ── UpsertImportedAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task Upsert_ExistingTransferRow_KeepsItsTypeAndPairing()
    {
        await SeedAsync(Row(1, type: "Transfer", amount: -500m, plaidId: "p1", transferAccountId: 2));

        await BuildSut().UpsertImportedAsync([Row(0, type: "Expense", amount: -505m, plaidId: "p1")]);

        var row = await ReloadAsync(1);
        Assert.Equal("Transfer", row.TransactionType);
        Assert.Equal(2, row.TransferAccountId);
        Assert.Equal(-505m, row.Amount);
    }

    [Fact]
    public async Task Upsert_ExistingIncomeOrExpenseRow_TakesTheIncomingType()
    {
        await SeedAsync(Row(1, type: "Income", amount: 40m, plaidId: "p1"));

        await BuildSut().UpsertImportedAsync([Row(0, type: "Expense", amount: 40m, plaidId: "p1")]);

        Assert.Equal("Expense", (await ReloadAsync(1)).TransactionType);
    }

    // ── Reads ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIds_SkipsRowsOwnedBySomeoneElse()
    {
        await SeedAsync(Row(1), Row(2, accountId: 3));

        var rows = await BuildSut().GetByIdsAsync([1, 2], UserId);

        Assert.Equal([1], rows.Select(r => r.Id));
    }

    [Fact]
    public async Task GetByPlaidTransactionIds_ReturnsOnlyMatchingRows()
    {
        await SeedAsync(Row(1, plaidId: "p1"), Row(2, plaidId: "p2"), Row(3));

        var rows = await BuildSut().GetByPlaidTransactionIdsAsync(["p2", "missing"]);

        Assert.Equal([2], rows.Select(r => r.Id));
    }

    [Fact]
    public async Task GetFiltered_ByPlaidAccountAndCategory()
    {
        await SeedAsync(
            Row(1, plaidAccountId: "chk", categoryId: 3),
            Row(2, plaidAccountId: "sav", categoryId: 3),
            Row(3, plaidAccountId: "chk", categoryId: 4));

        var byAccount = await BuildSut().GetFilteredAsync(UserId, new TransactionFilter(PlaidAccountId: "chk"));
        var byCategory = await BuildSut().GetFilteredAsync(UserId, new TransactionFilter(CategoryId: 3));
        var both = await BuildSut().GetFilteredAsync(UserId, new TransactionFilter(PlaidAccountId: "chk", CategoryId: 3));

        Assert.Equal([1, 3], byAccount.Select(r => r.Id).Order());
        Assert.Equal([1, 2], byCategory.Select(r => r.Id).Order());
        Assert.Equal([1], both.Select(r => r.Id));
    }

    [Fact]
    public async Task GetTransferCandidates_ArePostedOwnedRowsInTheGivenCategories_IncludingPairedOnes()
    {
        await SeedAsync(
            Row(1, plaidCategory: "TRANSFER_OUT"),
            Row(2, plaidCategory: "TRANSFER_IN", amount: 10m),
            Row(3, plaidCategory: "TRANSFER_OUT", pending: true),
            Row(4, plaidCategory: "TRANSFER_OUT", type: "Transfer", transferAccountId: 2),
            Row(5, plaidCategory: "FOOD_AND_DRINK"),
            Row(6, plaidCategory: "TRANSFER_OUT", accountId: 3));

        var rows = await BuildSut().GetTransferCandidatesAsync(UserId, ["TRANSFER_IN", "TRANSFER_OUT"]);

        Assert.Equal([1, 2, 4], rows.Select(r => r.Id).Order());
    }

    // ── Writes ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetCategory_WritesTheCategoryAndEachRowsType_OnOwnedRowsOnly()
    {
        await SeedAsync(Row(1, type: "Expense"), Row(2, type: "Transfer", transferAccountId: 2), Row(3, accountId: 3));

        var changed = await BuildSut().SetCategoryAsync(
            new Dictionary<int, string> { [1] = "Income", [2] = "Transfer", [3] = "Income" }, categoryId: 8, UserId);

        Assert.Equal(2, changed);
        var first = await ReloadAsync(1);
        Assert.Equal((8, "Income"), (first.CategoryId, first.TransactionType));
        var second = await ReloadAsync(2);
        Assert.Equal((8, "Transfer", 2), (second.CategoryId, second.TransactionType, second.TransferAccountId));
        var foreign = await ReloadAsync(3);
        Assert.Equal(((int?)null, "Expense"), (foreign.CategoryId, foreign.TransactionType));
    }

    [Fact]
    public async Task ApplyTransferUpdates_SetsTypeAndCounterpart_AndRevertsUnpairedLegs()
    {
        await SeedAsync(
            Row(1, accountId: 1, amount: -500m),
            Row(2, accountId: 2, amount: 500m),
            Row(3, type: "Transfer", transferAccountId: 2),
            Row(4));

        await BuildSut().ApplyTransferUpdatesAsync(
            [new TransferUpdate(1, "Transfer", 2), new TransferUpdate(2, "Transfer", 1), new TransferUpdate(3, "Income", null)]);

        var outflow = await ReloadAsync(1);
        var inflow = await ReloadAsync(2);
        var reverted = await ReloadAsync(3);
        Assert.Equal(("Transfer", (int?)2), (outflow.TransactionType, outflow.TransferAccountId));
        Assert.Equal(("Transfer", (int?)1), (inflow.TransactionType, inflow.TransferAccountId));
        Assert.Equal(("Income", (int?)null), (reverted.TransactionType, reverted.TransferAccountId));
        Assert.Equal("Expense", (await ReloadAsync(4)).TransactionType);
    }
}
