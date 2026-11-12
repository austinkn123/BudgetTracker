using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.Analysis;
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

    private static Transaction At(Transaction row, DateTime occurredAt)
    {
        row.OccurredAt = occurredAt;
        return row;
    }

    private static Transaction Paid(Transaction row, string payee)
    {
        row.Payee = payee;
        return row;
    }

    // ── GetLatestCategoryByPayeeAsync ───────────────────────────────────────

    [Fact]
    public async Task GetLatestCategoryByPayee_ReturnsTheNewestPickPerRequestedPayee_ForThisUserOnly()
    {
        await SeedAsync(
            Paid(Row(1, categoryId: 10), "Amazon"),
            At(Paid(Row(2, categoryId: 20), "amazon"), new DateTime(2026, 10, 2)),
            Paid(Row(3), "Target"),                                  // uncategorized
            Paid(Row(4, accountId: 3, categoryId: 30), "Walmart"),   // someone else's
            Paid(Row(5, accountId: 3, categoryId: 31), "Amazon"));   // someone else's, same payee

        var result = await BuildSut().GetLatestCategoryByPayeeAsync(UserId, ["AMAZON", "Target", "Walmart"]);

        Assert.Equal(20, Assert.Single(result).Value);
        Assert.Equal(20, result["Amazon"]);
    }

    [Fact]
    public async Task GetLatestCategoryByPayee_IgnoresPayeesOutsideTheBatch()
    {
        await SeedAsync(Paid(Row(1, categoryId: 10), "Amazon"), Paid(Row(2, categoryId: 11), "Costco"));

        var result = await BuildSut().GetLatestCategoryByPayeeAsync(UserId, ["costco"]);

        Assert.Equal(11, Assert.Single(result).Value);
    }

    [Fact]
    public async Task GetLatestCategoryByPayee_BreaksASameInstantTieByTheNewestId()
    {
        await SeedAsync(Paid(Row(7, categoryId: 10), "Amazon"), Paid(Row(8, categoryId: 20), "Amazon"));

        var result = await BuildSut().GetLatestCategoryByPayeeAsync(UserId, ["Amazon"]);

        Assert.Equal(20, result["amazon"]);
    }

    [Fact]
    public async Task GetLatestCategoryByPayee_EmptyOrBlankBatch_ReturnsNothing()
    {
        await SeedAsync(Paid(Row(1, categoryId: 10), "Amazon"));

        Assert.Empty(await BuildSut().GetLatestCategoryByPayeeAsync(UserId, []));
        Assert.Empty(await BuildSut().GetLatestCategoryByPayeeAsync(UserId, ["", "  "]));
    }

    // ── GetPageAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPage_ReturnsNewestFirst_TieBrokenByIdDescending_ForThisUserOnly()
    {
        var day = new DateTime(2026, 10, 5);
        await SeedAsync(
            At(Row(1), day.AddDays(-1)),
            At(Row(2), day),
            At(Row(3), day),
            At(Row(4), day.AddDays(1)),
            At(Row(5, accountId: 3), day.AddDays(2)));

        var rows = await BuildSut().GetPageAsync(UserId, new TransactionFilter(), after: null, take: 10);

        Assert.Equal([4, 3, 2, 1], rows.Select(r => r.Id));
    }

    [Fact]
    public async Task GetPage_TakesAtMostTheRequestedRows()
    {
        await SeedAsync(At(Row(1), new DateTime(2026, 10, 1)), At(Row(2), new DateTime(2026, 10, 2)), At(Row(3), new DateTime(2026, 10, 3)));

        var rows = await BuildSut().GetPageAsync(UserId, new TransactionFilter(), after: null, take: 2);

        Assert.Equal([3, 2], rows.Select(r => r.Id));
    }

    [Fact]
    public async Task GetPage_StartsStrictlyAfterTheCursor_IncludingSameInstantRowsWithLowerIds()
    {
        var day = new DateTime(2026, 10, 5);
        await SeedAsync(At(Row(1), day.AddDays(-1)), At(Row(2), day), At(Row(3), day), At(Row(4), day), At(Row(5), day.AddDays(1)));

        var rows = await BuildSut().GetPageAsync(UserId, new TransactionFilter(), new TransactionCursor(day, 3), take: 10);

        Assert.Equal([2, 1], rows.Select(r => r.Id));
    }

    [Fact]
    public async Task GetPage_WalkingEveryPage_VisitsEachRowOnce()
    {
        var day = new DateTime(2026, 10, 5);
        var seeded = Enumerable.Range(1, 23).Select(i => At(Row(i), day.AddDays(-(i % 4)))).ToArray();
        await SeedAsync(seeded);
        var sut = BuildSut();

        var seen = new List<int>();
        TransactionCursor? after = null;
        while (true)
        {
            var page = await sut.GetPageAsync(UserId, new TransactionFilter(), after, take: 5);
            seen.AddRange(page.Select(r => r.Id));
            if (page.Count < 5) break;
            after = new TransactionCursor(page[^1].OccurredAt, page[^1].Id);
        }

        var expected = seeded.OrderByDescending(r => r.OccurredAt).ThenByDescending(r => r.Id).Select(r => r.Id);
        Assert.Equal(expected, seen);
    }

    [Fact]
    public async Task GetPage_AppliesTheSameFiltersAsTheFilteredRead()
    {
        await SeedAsync(
            At(Row(1, plaidAccountId: "chk", categoryId: 3), new DateTime(2026, 9, 30)),
            At(Row(2, plaidAccountId: "chk", categoryId: 3), new DateTime(2026, 10, 1, 18, 0, 0)),
            At(Row(3, plaidAccountId: "sav", categoryId: 3), new DateTime(2026, 10, 2)),
            At(Row(4, plaidAccountId: "chk"), new DateTime(2026, 10, 3)),
            At(Row(5, plaidAccountId: "chk", categoryId: 3, pending: true), new DateTime(2026, 10, 31, 23, 59, 0)),
            At(Row(6, plaidAccountId: "chk", categoryId: 3), new DateTime(2026, 11, 1)));

        var rows = await BuildSut().GetPageAsync(
            UserId,
            new TransactionFilter(From: new DateTime(2026, 10, 1), To: new DateTime(2026, 10, 31), PlaidAccountId: "chk", CategoryId: 3),
            after: null,
            take: 10);
        var notPending = await BuildSut().GetPageAsync(UserId, new TransactionFilter(IsPending: false, Uncategorized: true), null, 10);

        Assert.Equal([5, 2], rows.Select(r => r.Id));
        Assert.Equal([4], notPending.Select(r => r.Id));
    }

    // ── GetFilteredAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetFiltered_ToOnTheLastRepresentableDay_DoesNotOverflow()
    {
        await SeedAsync(At(Row(1), new DateTime(2026, 10, 1)));

        var rows = await BuildSut().GetFilteredAsync(UserId, new TransactionFilter(From: new DateTime(1970, 1, 1), To: new DateTime(9999, 12, 31)));

        Assert.Equal([1], rows.Select(r => r.Id));
    }

    // ── Uncategorized filter: "needs a category" ────────────────────────────

    [Fact]
    public async Task Should_ExcludeTransfers_When_FilteringForUncategorizedRows()
    {
        await SeedAsync(
            Row(1),                                                  // needs a category
            Row(2, type: "Income", amount: 40m),                     // needs a category
            Row(3, type: "Transfer", transferAccountId: 2),          // paired transfer: never waits on a category
            Row(4, categoryId: 9),                                   // categorized
            Row(5, accountId: 3));                                   // someone else's

        var filtered = await BuildSut().GetFilteredAsync(UserId, new TransactionFilter(Uncategorized: true));
        var page = await BuildSut().GetPageAsync(UserId, new TransactionFilter(Uncategorized: true), null, 10);

        Assert.Equal([1, 2], filtered.Select(r => r.Id).Order());
        Assert.Equal([2, 1], page.Select(r => r.Id));
    }

    [Fact]
    public async Task Should_MatchTheSummaryCount_When_FilteringForUncategorizedRows()
    {
        await SeedAsync(
            Row(1), Row(2, type: "Income", amount: 40m), Row(3, type: "Transfer", transferAccountId: 2),
            Row(4, type: "Transfer", transferAccountId: 1, accountId: 2), Row(5, categoryId: 9));

        var summary = await BuildSut().GetSummaryAsync(UserId);
        var backlog = await BuildSut().GetFilteredAsync(UserId, new TransactionFilter(Uncategorized: true));

        Assert.Equal(summary.UncategorizedCount, backlog.Count());
    }

    [Fact]
    public async Task Should_StillReturnEveryCategorizedRow_When_UncategorizedIsFalse()
    {
        await SeedAsync(
            Row(1), Row(2, type: "Transfer", transferAccountId: 2), Row(3, categoryId: 9),
            Row(4, type: "Transfer", transferAccountId: 2, categoryId: 9));

        var categorized = await BuildSut().GetFilteredAsync(UserId, new TransactionFilter(Uncategorized: false));

        Assert.Equal([3, 4], categorized.Select(r => r.Id).Order());
    }

    // ── GetTotalsByCategoryAndTypeAsync ─────────────────────────────────────

    [Fact]
    public async Task Should_SumSignedAmountsPerCategoryAndType_When_TotallingAWindow()
    {
        var day = new DateTime(2026, 10, 10);
        await SeedAsync(
            At(Row(1, categoryId: 3, amount: -80m), day),
            At(Row(2, categoryId: 3, amount: 30m), day),                                   // refund nets in SQL
            At(Row(3, categoryId: 3, type: "Income", amount: 500m), day),
            At(Row(4, amount: -12m), day),                                                 // uncategorized
            At(Row(5, amount: -3m), day),
            At(Row(6, type: "Transfer", amount: -200m, transferAccountId: 2), day),
            At(Row(7, categoryId: 3, amount: -1000m, accountId: 3), day));                 // someone else's

        var totals = await BuildSut().GetTotalsByCategoryAndTypeAsync(UserId, day, day);

        Assert.Equal(
            new[]
            {
                new CategoryTypeTotal(null, "Expense", -15m),
                new CategoryTypeTotal(null, "Transfer", -200m),
                new CategoryTypeTotal(3, "Expense", -50m),
                new CategoryTypeTotal(3, "Income", 500m)
            },
            totals.OrderBy(t => t.CategoryId).ThenBy(t => t.TransactionType));
    }

    [Fact]
    public async Task Should_IncludeBothEndDaysWholly_When_TotallingAWindow()
    {
        var from = new DateTime(2026, 10, 1);
        var to = new DateTime(2026, 10, 31);
        await SeedAsync(
            At(Row(1, categoryId: 3, amount: -1m), from.AddMinutes(-1)),               // just before
            At(Row(2, categoryId: 3, amount: -2m), from),                              // first instant of from
            At(Row(3, categoryId: 3, amount: -3m), to.AddHours(23).AddMinutes(59)),    // 23:59 on to
            At(Row(4, categoryId: 3, amount: -4m), to.AddDays(1)));                    // just after

        // A time component on the bounds is ignored: whole days, as the filtered read does.
        var totals = await BuildSut().GetTotalsByCategoryAndTypeAsync(UserId, from.AddHours(15), to.AddHours(1));

        Assert.Equal(new CategoryTypeTotal(3, "Expense", -5m), Assert.Single(totals));
    }

    [Fact]
    public async Task Should_NotOverflow_When_TotallingTheAllTimeRange()
    {
        await SeedAsync(At(Row(1, categoryId: 3, amount: -7m), new DateTime(1970, 1, 1)), At(Row(2, categoryId: 3, amount: -1m), new DateTime(2026, 10, 1)));

        var totals = await BuildSut().GetTotalsByCategoryAndTypeAsync(UserId, new DateTime(1970, 1, 1), new DateTime(9999, 12, 31));

        Assert.Equal(new CategoryTypeTotal(3, "Expense", -8m), Assert.Single(totals));
    }

    [Fact]
    public async Task Should_ReturnNothing_When_TotallingAnEmptyLedger()
    {
        Assert.Empty(await BuildSut().GetTotalsByCategoryAndTypeAsync(UserId, new DateTime(1970, 1, 1), new DateTime(9999, 12, 31)));
    }

    // ── GetSummaryAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummary_CountsUncategorizedNonTransferRows_AndSumsPendingPerPlaidAccount()
    {
        await SeedAsync(
            Row(1),                                                         // uncategorized: counts
            Row(2, type: "Income", amount: 40m),                            // uncategorized: counts
            Row(3, type: "Transfer", transferAccountId: 2),                 // paired transfer: never waits on a category
            Row(4, categoryId: 9),                                          // categorized
            Row(5, accountId: 3),                                           // someone else's
            Row(6, categoryId: 9, pending: true, amount: -12.50m, plaidAccountId: "chk"),
            Row(7, categoryId: 9, pending: true, amount: -7.25m, plaidAccountId: "chk"),
            Row(8, categoryId: 9, pending: true, amount: 100m, plaidAccountId: "card"),
            Row(9, categoryId: 9, pending: true, amount: -1m, plaidAccountId: null),     // manual row: no account key
            Row(10, categoryId: 9, pending: true, amount: -99m, plaidAccountId: "chk", accountId: 3)); // someone else's

        var summary = await BuildSut().GetSummaryAsync(UserId);

        Assert.Equal(2, summary.UncategorizedCount);
        Assert.Equal(
            [new PendingAccountTotal("card", 100m), new PendingAccountTotal("chk", -19.75m)],
            summary.PendingByAccount);
    }

    [Fact]
    public async Task GetSummary_EmptyLedger_IsZeroAndNoAccounts()
    {
        var summary = await BuildSut().GetSummaryAsync(UserId);

        Assert.Equal(0, summary.UncategorizedCount);
        Assert.Empty(summary.PendingByAccount);
    }

    // ── GetCategoryUsageAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetCategoryUsage_CountsAllTime_AndSplitsFlowsSinceTheInstant()
    {
        var since = new DateTime(2026, 9, 1, 12, 0, 0);
        await SeedAsync(
            At(Row(1, categoryId: 3, amount: -10m), since.AddDays(-30)),   // before since: count only
            At(Row(2, categoryId: 3, amount: -20m), since),                // on since: outflow
            At(Row(3, categoryId: 3, amount: 5m), since.AddDays(1)),       // inflow
            At(Row(4, categoryId: 3, amount: 50m), since.AddMinutes(-1)),  // before since
            At(Row(5, categoryId: 4, amount: 7m), since.AddDays(-400)),    // only old rows
            At(Row(6, amount: -3m), since.AddDays(2)),                     // uncategorized: no entry
            At(Row(7, categoryId: 3, amount: -1000m, accountId: 3), since.AddDays(3))); // someone else's

        var usage = await BuildSut().GetCategoryUsageAsync(UserId, since);

        Assert.Equal(
            [new CategoryUsage(3, 4, 5m, -20m), new CategoryUsage(4, 1, 0m, 0m)],
            usage);
    }

    [Fact]
    public async Task GetCategoryUsage_NoRows_IsEmpty()
    {
        Assert.Empty(await BuildSut().GetCategoryUsageAsync(UserId, new DateTime(2026, 1, 1)));
    }

    // ── GetCategoryIdsInUseAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetCategoryIdsInUse_IsEveryDistinctCategoryIncludingUncategorized_ForThisUserOnly()
    {
        await SeedAsync(Row(1, categoryId: 3), Row(2, categoryId: 3), Row(3), Row(4, categoryId: 5), Row(5, accountId: 3, categoryId: 9));

        var ids = await BuildSut().GetCategoryIdsInUseAsync(UserId);

        Assert.Equal(new int?[] { null, 3, 5 }, ids.Order());
    }

    // ── GetPostedSumByPlaidAccountAfterAsync ────────────────────────────────

    [Fact]
    public async Task GetPostedSumByPlaidAccountAfter_SumsPostedRowsOnLaterDays_PerAccount()
    {
        var to = new DateTime(2026, 10, 15);
        await SeedAsync(
            At(Row(1, amount: -10m, plaidAccountId: "chk"), to.AddHours(23)),             // on To: excluded
            At(Row(2, amount: -20m, plaidAccountId: "chk"), to.AddDays(1)),
            At(Row(3, amount: 5m, plaidAccountId: "chk"), to.AddDays(9)),
            At(Row(4, amount: -7m, plaidAccountId: "card", type: "Transfer", transferAccountId: 2), to.AddDays(2)),
            At(Row(5, amount: -99m, plaidAccountId: "chk", pending: true), to.AddDays(2)), // pending: excluded
            At(Row(6, amount: -1m, plaidAccountId: null), to.AddDays(2)),                  // manual: excluded
            At(Row(7, amount: -50m, plaidAccountId: "chk", accountId: 3), to.AddDays(2))); // someone else's

        var sums = await BuildSut().GetPostedSumByPlaidAccountAfterAsync(UserId, to);

        Assert.Equal(2, sums.Count);
        Assert.Equal(-15m, sums["chk"]);
        Assert.Equal(-7m, sums["card"]);
    }

    [Fact]
    public async Task GetPostedSumByPlaidAccountAfter_TheLastRepresentableDay_IsEmpty()
    {
        await SeedAsync(At(Row(1, amount: -10m), new DateTime(9999, 12, 31)));

        Assert.Empty(await BuildSut().GetPostedSumByPlaidAccountAfterAsync(UserId, new DateTime(9999, 12, 31)));
    }

    // ── GetNewestIncomeAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetNewestIncome_IsNewestFirstIncomeInTheCategoriesOnOrBeforeTheDay()
    {
        var today = new DateTime(2026, 10, 15);
        await SeedAsync(
            At(Row(1, type: "Income", amount: 2000m, categoryId: 7), today.AddDays(-28)),
            At(Row(2, type: "Income", amount: 2000m, categoryId: 7), today.AddDays(-14)),
            At(Row(3, type: "Income", amount: 2000m, categoryId: 7), today.AddHours(20)),     // on today: included
            At(Row(4, type: "Income", amount: 2000m, categoryId: 7), today.AddDays(1)),       // future: excluded
            At(Row(5, type: "Income", amount: 2000m, categoryId: 8), today.AddDays(-1)),      // other category
            At(Row(6, type: "Expense", amount: 2000m, categoryId: 7), today.AddDays(-2)),     // not income
            At(Row(7, type: "Income", amount: 50m, categoryId: 7), today.AddDays(-3)),        // small
            At(Row(8, type: "Income", amount: 2000m, categoryId: 7, accountId: 3), today));   // someone else's

        var all = await BuildSut().GetNewestIncomeAsync(UserId, [7], today, minAmount: null, take: 10);
        var top2 = await BuildSut().GetNewestIncomeAsync(UserId, [7], today, minAmount: null, take: 2);
        var large = await BuildSut().GetNewestIncomeAsync(UserId, [7], today, minAmount: 1000m, take: 10);

        Assert.Equal([3, 7, 2, 1], all.Select(r => r.Id));
        Assert.Equal([3, 7], top2.Select(r => r.Id));
        Assert.Equal([3, 2, 1], large.Select(r => r.Id));
    }

    [Fact]
    public async Task GetNewestIncome_NoCategories_IsEmpty()
    {
        await SeedAsync(Row(1, type: "Income", amount: 10m, categoryId: 7));

        Assert.Empty(await BuildSut().GetNewestIncomeAsync(UserId, [], new DateTime(2026, 12, 1), null, 10));
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

    [Fact]
    public async Task GetTransferCandidates_CoverTheWholeHistory_WithTheFieldsPairingReads()
    {
        var old = At(Row(1, plaidCategory: "TRANSFER_OUT", amount: -500m, categoryId: 7, type: "Transfer", transferAccountId: 2), new DateTime(2019, 3, 4));
        await SeedAsync(old, At(Row(2, plaidCategory: "TRANSFER_IN", amount: 500m, accountId: 2, plaidAccountId: "sav"), new DateTime(2026, 10, 30)));

        var rows = (await BuildSut().GetTransferCandidatesAsync(UserId, ["TRANSFER_IN", "TRANSFER_OUT"])).OrderBy(r => r.Id).ToList();

        Assert.Equal([1, 2], rows.Select(r => r.Id));
        var first = rows[0];
        Assert.Equal(
            (1, "chk", -500m, new DateTime(2019, 3, 4), "TRANSFER_OUT", false, "Transfer", (int?)2, (int?)7),
            (first.AccountId, first.PlaidAccountId, first.Amount, first.OccurredAt, first.PlaidCategoryPrimary, first.IsPending,
                first.TransactionType, first.TransferAccountId, first.CategoryId));
        Assert.Empty(_context.ChangeTracker.Entries());
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
