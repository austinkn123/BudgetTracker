using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.Analysis;
using BudgetTracker.Server.Utilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static BudgetTracker.Tests.Postgres.PostgresSeed;

namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// <see cref="TransactionAccessor"/> against real PostgreSQL: the SQL Npgsql generates for the keyset seek, ILIKE search,
/// day bounds, grouped aggregates and the per-payee lookup, none of which the InMemory provider executes as SQL.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Postgres")]
public sealed class TransactionAccessorPostgresTests(PostgresFixture fixture)
{
    private const string Expense = "Expense";
    private const string Income = "Income";
    private const string Transfer = "Transfer";

    private static readonly TransactionEngine Engine = new();
    private static readonly TransactionFilter NoFilter = new();

    /// <summary>
    /// Planner switches for the tests that pin an ORDER BY. On these small tables PostgreSQL otherwise picks plans that
    /// return rows in the asserted order as a side effect, so the test would pass with the ORDER BY removed.
    /// </summary>
    private static class Planner
    {
        /// <summary>
        /// No index scans: IX_Transactions_AccountId_LowerPayee_OccurredAt ends in ("OccurredAt" DESC, "Id" DESC) and
        /// would hand the per-payee lookup its rows already tie-broken by id.
        /// </summary>
        public const string NoIndexScans =
            "SET enable_indexscan = off; SET enable_indexonlyscan = off; SET enable_bitmapscan = off";

        /// <summary>No sort-based grouping: GROUP BY runs as a HashAggregate, whose output is in hash order, not key order.</summary>
        public const string HashedGrouping = "SET enable_sort = off";
    }

    /// <param name="act">The accessor call under test.</param>
    /// <param name="plannerSettings">
    /// Optional <see cref="Planner"/> SET statements for this call's session only: the connection is held open for the
    /// call and Npgsql resets session state when it goes back to the pool.
    /// </param>
    private async Task<T> WithAccessorAsync<T>(Func<TransactionAccessor, Task<T>> act, string? plannerSettings = null)
    {
        await using var context = fixture.CreateContext();
        if (plannerSettings is not null)
        {
            await context.Database.OpenConnectionAsync();
            await context.Database.ExecuteSqlRawAsync(plannerSettings);
        }

        return await act(new TransactionAccessor(context));
    }

    /// <summary>
    /// Pages the ledger the way TransactionManager does: fetch one past the page, let the engine cut the page and encode
    /// the cursor, then parse that opaque cursor back for the next fetch.
    /// </summary>
    private async Task<List<Transaction>> WalkAsync(int userId, TransactionFilter filter, int pageSize)
    {
        var walked = new List<Transaction>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var after = Engine.ParseCursor(cursor);
            Assert.True(after.IsSuccess);

            var fetched = await WithAccessorAsync(a => a.GetPageAsync(userId, filter, after.Value, pageSize + 1));
            var page = Engine.BuildPage(fetched, pageSize);

            Assert.True(page.Items.Count <= pageSize);
            walked.AddRange(page.Items);
            cursor = page.NextCursor;
            Assert.True(++pages <= 1000, "The cursor walk did not terminate.");
        } while (cursor is not null);

        return walked;
    }

    private static int[] NewestFirstIds(IEnumerable<Transaction> rows) =>
        rows.OrderByDescending(t => t.OccurredAt).ThenByDescending(t => t.Id).Select(t => t.Id).ToArray();

    // ── GetPageAsync ────────────────────────────────────────────────────────

    [PostgresFact]
    public async Task Should_ReturnEveryRowOnceNewestFirstWithIdTiebreak_When_WalkingCursorAcrossMicrosecondTies()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var other = await fixture.UserAsync();
        var otherAccount = await fixture.AccountAsync(other.Id);

        // Three rows per instant, instants one microsecond apart, page size 4: every page boundary but a few lands inside
        // a tie group, and the seek has to tell adjacent microseconds apart.
        var start = new DateTime(2026, 3, 10, 12, 0, 0).AddTicks(1_234_560);
        var rows = Enumerable.Range(0, 25)
            .Select(i => Row(account.Id, start.AddTicks(TimeSpan.TicksPerMicrosecond * (i / 3))))
            .ToArray();
        await fixture.TransactionsAsync(rows);
        await fixture.TransactionsAsync(Row(otherAccount.Id, start), Row(otherAccount.Id, start.AddTicks(TimeSpan.TicksPerMicrosecond)));

        var walked = await WalkAsync(user.Id, NoFilter, pageSize: 4);

        Assert.Equal(NewestFirstIds(rows), walked.Select(t => t.Id).ToArray());
        Assert.Equal(
            rows.Select(t => (t.Id, t.OccurredAt.Ticks)).OrderBy(x => x.Id),
            walked.Select(t => (t.Id, t.OccurredAt.Ticks)).OrderBy(x => x.Id));
    }

    [PostgresFact]
    public async Task Should_NotSkipOrRepeatRows_When_SeededTimestampsAreFinerThanAMicrosecond()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);

        // .NET ticks are 100 ns, Postgres timestamps 1 µs: the first three collapse to one stored instant.
        var start = new DateTime(2026, 3, 11, 8, 30, 0).AddTicks(5_000_000);
        var rows = new[] { 1, 5, 9, 10, 11, 20 }.Select(ticks => Row(account.Id, start.AddTicks(ticks))).ToArray();
        await fixture.TransactionsAsync(rows);

        var walked = await WalkAsync(user.Id, NoFilter, pageSize: 1);

        Assert.Equal(rows.Length, walked.Count);
        Assert.Equal(rows.Select(t => t.Id).Order(), walked.Select(t => t.Id).Order());
        Assert.All(walked, t => Assert.Equal(0, t.OccurredAt.Ticks % TimeSpan.TicksPerMicrosecond));
        Assert.Equal(NewestFirstIds(walked), walked.Select(t => t.Id).ToArray());
    }

    [PostgresFact]
    public async Task Should_WalkOnlyMatchingRows_When_FiltersAreCombinedWithTheCursor()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var other = await fixture.UserAsync();
        var otherAccount = await fixture.AccountAsync(other.Id);

        var day = new DateTime(2026, 4, 15);
        var matching = Enumerable.Range(0, 7)
            .Select(i => Row(account.Id, day.AddHours(i / 2), payee: i % 2 == 0 ? "Corner COFFEE" : "coffee cart", imported: true))
            .ToArray();
        await fixture.TransactionsAsync(matching);
        await fixture.TransactionsAsync(
            Row(account.Id, day.AddHours(1), payee: "Corner Coffee", imported: true, pending: true),
            Row(account.Id, day.AddHours(1), payee: "Corner Coffee", imported: false),
            Row(account.Id, day.AddHours(1), payee: "Tea House", imported: true),
            Row(account.Id, day.AddDays(1), payee: "Corner Coffee", imported: true),
            Row(account.Id, day.AddDays(-1), payee: "Corner Coffee", imported: true),
            Row(otherAccount.Id, day.AddHours(1), payee: "Corner Coffee", imported: true));

        var filter = new TransactionFilter(From: day, To: day, IsImported: true, IsPending: false, Search: "Coffee");
        var walked = await WalkAsync(user.Id, filter, pageSize: 3);

        Assert.Equal(NewestFirstIds(matching), walked.Select(t => t.Id).ToArray());
    }

    [PostgresFact]
    public async Task Should_AcceptUtcKindCursor_When_SeekingAPage()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var newer = Row(account.Id, new DateTime(2026, 4, 2, 9, 0, 0));
        var older = Row(account.Id, new DateTime(2026, 4, 1, 9, 0, 0));
        await fixture.TransactionsAsync(newer, older);

        var after = new TransactionCursor(DateTime.SpecifyKind(newer.OccurredAt, DateTimeKind.Utc), newer.Id);
        var page = await WithAccessorAsync(a => a.GetPageAsync(user.Id, NoFilter, after, 10));

        Assert.Equal([older.Id], page.Select(t => t.Id).ToArray());
    }

    [PostgresFact]
    public async Task Should_ReturnOnlyTheNewestTakeRows_When_MoreRowsMatchThanTake()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var day = new DateTime(2026, 4, 3, 9, 0, 0);
        var rows = Enumerable.Range(0, 5).Select(i => Row(account.Id, day.AddHours(i))).ToArray();
        await fixture.TransactionsAsync(rows);

        var page = await WithAccessorAsync(a => a.GetPageAsync(user.Id, NoFilter, null, take: 2));

        Assert.Equal([rows[4].Id, rows[3].Id], page.Select(t => t.Id).ToArray());
    }

    // ── GetFilteredAsync ────────────────────────────────────────────────────

    [PostgresFact]
    public async Task Should_MatchPayeeAndNotesIgnoringCase_When_Searching()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var at = new DateTime(2026, 5, 1, 10, 0, 0);
        var byPayee = Row(account.Id, at.AddMinutes(2), payee: "Blue Bottle COFFEE");
        var byNotes = Row(account.Id, at.AddMinutes(1), payee: "Square", notes: "weekly coffee run");
        var miss = Row(account.Id, at, payee: "Tea House", notes: "oolong");
        var blank = Row(account.Id, at);
        await fixture.TransactionsAsync(byPayee, byNotes, miss, blank);

        var found = await WithAccessorAsync(a => a.GetFilteredAsync(user.Id, new TransactionFilter(Search: "  cOfFeE ")));

        Assert.Equal([byPayee.Id, byNotes.Id], found.Select(t => t.Id).ToArray());
    }

    [PostgresFact]
    public async Task Should_TreatLikeWildcardsLiterally_When_SearchContainsPercentUnderscoreOrBackslash()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var at = new DateTime(2026, 5, 2, 10, 0, 0);
        var percent = Row(account.Id, at, payee: "100% Juice");
        var noPercent = Row(account.Id, at, payee: "100 Juice");
        var underscore = Row(account.Id, at, payee: "save_file");
        var noUnderscore = Row(account.Id, at, payee: "savexfile");
        var backslash = Row(account.Id, at, notes: @"C:\receipts");
        var noBackslash = Row(account.Id, at, notes: "C:receipts");
        await fixture.TransactionsAsync(percent, noPercent, underscore, noUnderscore, backslash, noBackslash);

        async Task<int[]> SearchAsync(string term) =>
            (await WithAccessorAsync(a => a.GetFilteredAsync(user.Id, new TransactionFilter(Search: term)))).Select(t => t.Id).ToArray();

        Assert.Equal([percent.Id], await SearchAsync("100%"));
        Assert.Equal([underscore.Id], await SearchAsync("SAVE_FILE"));
        Assert.Equal([backslash.Id], await SearchAsync(@"c:\rec"));
        Assert.Empty(await SearchAsync("%_"));
    }

    [PostgresFact]
    public async Task Should_IncludeTheWholeFromAndToDays_When_FilteringByDateRange()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var from = new DateTime(2026, 6, 1);
        var to = new DateTime(2026, 6, 30);
        var lastInstant = to.AddDays(1).AddTicks(-TimeSpan.TicksPerMicrosecond);

        var firstMidnight = Row(account.Id, from);
        var lastMicrosecond = Row(account.Id, lastInstant);
        var before = Row(account.Id, from.AddTicks(-TimeSpan.TicksPerMicrosecond));
        var after = Row(account.Id, to.AddDays(1));
        await fixture.TransactionsAsync(firstMidnight, lastMicrosecond, before, after);

        // Time components on the bounds are ignored.
        var filter = new TransactionFilter(From: from.AddHours(15.5), To: to.AddHours(8));
        var found = await WithAccessorAsync(a => a.GetFilteredAsync(user.Id, filter));

        Assert.Equal([lastMicrosecond.Id, firstMidnight.Id], found.Select(t => t.Id).ToArray());
        Assert.Equal(lastInstant, found.First().OccurredAt);
    }

    [PostgresFact]
    public async Task Should_LeaveTransfersOutOfTheUncategorizedBacklog_When_FilteringByUncategorized()
    {
        var user = await fixture.UserAsync();
        var checking = await fixture.AccountAsync(user.Id, "Checking");
        var savings = await fixture.AccountAsync(user.Id, "Savings");
        var category = await fixture.CategoryAsync(user.Id, "Groceries");
        var at = new DateTime(2026, 6, 5, 12, 0, 0);

        var needsCategory = Row(checking.Id, at);
        var transfer = Row(checking.Id, at, type: Transfer, transferAccountId: savings.Id);
        var categorized = Row(checking.Id, at, categoryId: category.Id);
        await fixture.TransactionsAsync(needsCategory, transfer, categorized);

        var backlog = await WithAccessorAsync(a => a.GetFilteredAsync(user.Id, new TransactionFilter(Uncategorized: true)));
        var filed = await WithAccessorAsync(a => a.GetFilteredAsync(user.Id, new TransactionFilter(Uncategorized: false)));

        Assert.Equal([needsCategory.Id], backlog.Select(t => t.Id).ToArray());
        Assert.Equal([categorized.Id], filed.Select(t => t.Id).ToArray());
    }

    [PostgresFact]
    public async Task Should_NotThrow_When_UtcKindBoundsArePassedThroughWallClockUnzoned()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var inside = Row(account.Id, new DateTime(2026, 7, 4, 23, 30, 0));
        var outside = Row(account.Id, new DateTime(2026, 7, 5, 0, 0, 0));
        await fixture.TransactionsAsync(inside, outside);

        // What the endpoint does with "?from=2026-07-04T00:00:00Z&to=2026-07-04T00:00:00Z".
        var bound = new DateTime(2026, 7, 4, 0, 0, 0, DateTimeKind.Utc);
        var filter = new TransactionFilter(From: WallClock.Unzoned((DateTime?)bound), To: WallClock.Unzoned((DateTime?)bound));

        var found = await WithAccessorAsync(a => a.GetFilteredAsync(user.Id, filter));
        var totals = await WithAccessorAsync(a => a.GetTotalsByCategoryAndTypeAsync(user.Id, WallClock.Unzoned(bound), WallClock.Unzoned(bound)));

        Assert.Equal([inside.Id], found.Select(t => t.Id).ToArray());
        Assert.Equal([new CategoryTypeTotal(null, Expense, -10m)], totals);
    }

    // ── Aggregates over a mixed ledger ──────────────────────────────────────

    private sealed record Ledger(int UserId, int Groceries, int Salary, int Unused, Transaction FebSalary, Transaction JanSalary);

    /// <summary>
    /// One user's ledger with every shape the aggregates distinguish, next to a second user whose rows must never count.
    /// <code>
    ///  #  when                        type      amount    category   plaid  pending
    ///  1  01-05 09:00                 Expense    -50.25   Groceries  pa-1
    ///  2  01-20 00:00                 Expense    -20.00   Groceries  pa-1
    ///  3  02-03 12:00                 Expense     +5.00   Groceries  pa-1            (refund)
    ///  4  02-10 18:00                 Income   +2000.00   Salary     pa-1
    ///  5  01-15 09:00                 Income   +1500.00   Salary     pa-2
    ///  6  02-12 10:00                 Expense    -12.34   -          pa-1   yes
    ///  7  02-13 10:00                 Expense     -7.66   -          pa-2   yes
    ///  8  02-14 23:59:59.999999       Expense     -1.00   -          pa-1   yes
    ///  9  02-01 08:00                 Transfer  -300.00   -          pa-1            (checking -> savings)
    /// 10  02-01 08:00                 Transfer  +300.00   -          pa-2            (savings &lt;- checking)
    /// 11  02-20 10:00                 Expense     -9.99   -          -               (manual)
    /// 12  02-11 10:00                 Expense    -40.00   Salary     pa-1            (clawback filed under Salary)
    /// </code>
    /// </summary>
    private async Task<Ledger> SeedMixedLedgerAsync()
    {
        var user = await fixture.UserAsync();
        var checking = await fixture.AccountAsync(user.Id, "Checking");
        var savings = await fixture.AccountAsync(user.Id, "Savings");
        var groceries = await fixture.CategoryAsync(user.Id, "Groceries");
        var salary = await fixture.CategoryAsync(user.Id, "Salary", Income);
        var unused = await fixture.CategoryAsync(user.Id, "Unused");

        var febSalary = Row(checking.Id, new DateTime(2026, 2, 10, 18, 0, 0), 2000.00m, Income, salary.Id, plaidAccountId: "pa-1");
        var janSalary = Row(savings.Id, new DateTime(2026, 1, 15, 9, 0, 0), 1500.00m, Income, salary.Id, plaidAccountId: "pa-2");
        await fixture.TransactionsAsync(
            Row(checking.Id, new DateTime(2026, 1, 5, 9, 0, 0), -50.25m, Expense, groceries.Id, plaidAccountId: "pa-1"),
            Row(checking.Id, new DateTime(2026, 1, 20), -20.00m, Expense, groceries.Id, plaidAccountId: "pa-1"),
            Row(checking.Id, new DateTime(2026, 2, 3, 12, 0, 0), 5.00m, Expense, groceries.Id, plaidAccountId: "pa-1"),
            febSalary,
            janSalary,
            Row(checking.Id, new DateTime(2026, 2, 12, 10, 0, 0), -12.34m, plaidAccountId: "pa-1", pending: true),
            Row(savings.Id, new DateTime(2026, 2, 13, 10, 0, 0), -7.66m, plaidAccountId: "pa-2", pending: true),
            Row(checking.Id, new DateTime(2026, 2, 15).AddTicks(-TimeSpan.TicksPerMicrosecond), -1.00m, plaidAccountId: "pa-1", pending: true),
            Row(checking.Id, new DateTime(2026, 2, 1, 8, 0, 0), -300.00m, Transfer, plaidAccountId: "pa-1", transferAccountId: savings.Id),
            Row(savings.Id, new DateTime(2026, 2, 1, 8, 0, 0), 300.00m, Transfer, plaidAccountId: "pa-2", transferAccountId: checking.Id),
            Row(checking.Id, new DateTime(2026, 2, 20, 10, 0, 0), -9.99m),
            Row(checking.Id, new DateTime(2026, 2, 11, 10, 0, 0), -40.00m, Expense, salary.Id, plaidAccountId: "pa-1"));

        var other = await fixture.UserAsync();
        var otherAccount = await fixture.AccountAsync(other.Id);
        var otherSalary = await fixture.CategoryAsync(other.Id, "Salary", Income);
        await fixture.TransactionsAsync(
            Row(otherAccount.Id, new DateTime(2026, 2, 10, 19, 0, 0), 9999.00m, Income, otherSalary.Id, plaidAccountId: "pa-1"),
            Row(otherAccount.Id, new DateTime(2026, 2, 12, 10, 0, 0), -777.00m, plaidAccountId: "pa-1", pending: true));

        return new Ledger(user.Id, groceries.Id, salary.Id, unused.Id, febSalary, janSalary);
    }

    [PostgresFact]
    public async Task Should_CountBacklogAndSumPendingPerPlaidAccount_When_GettingSummary()
    {
        var ledger = await SeedMixedLedgerAsync();

        var summary = await WithAccessorAsync(a => a.GetSummaryAsync(ledger.UserId));

        // Rows 6, 7, 8 and 11; the two uncategorized Transfer legs do not count.
        Assert.Equal(4, summary.UncategorizedCount);
        Assert.Equal(
            [new PendingAccountTotal("pa-1", -13.34m), new PendingAccountTotal("pa-2", -7.66m)],
            summary.PendingByAccount);
    }

    [PostgresFact]
    public async Task Should_CountAllTimeAndSumFlowsFromSinceInclusive_When_GettingCategoryUsage()
    {
        var ledger = await SeedMixedLedgerAsync();

        // Row 2 sits exactly on the instant, so it counts; rows 1 and 5 are before it.
        var usage = await WithAccessorAsync(a => a.GetCategoryUsageAsync(ledger.UserId, new DateTime(2026, 1, 20)));

        CategoryUsage[] expected =
        [
            new(ledger.Groceries, 3, 5.00m, -20.00m),
            new(ledger.Salary, 3, 2000.00m, -40.00m)
        ];
        Assert.Equal(expected.OrderBy(u => u.CategoryId), usage);
    }

    [PostgresFact]
    public async Task Should_SumPerCategoryAndTypeOverWholeDays_When_GettingTotalsByCategoryAndType()
    {
        var ledger = await SeedMixedLedgerAsync();

        var totals = await WithAccessorAsync(a =>
            a.GetTotalsByCategoryAndTypeAsync(ledger.UserId, new DateTime(2026, 2, 1, 13, 0, 0), new DateTime(2026, 2, 14, 1, 0, 0)));

        CategoryTypeTotal[] expected =
        [
            new(ledger.Groceries, Expense, 5.00m),
            new(ledger.Salary, Income, 2000.00m),
            new(ledger.Salary, Expense, -40.00m),
            new(null, Expense, -21.00m),
            new(null, Transfer, 0.00m)
        ];
        Assert.Equal(expected.Length, totals.Count);
        Assert.Equal(expected.ToHashSet(), totals.ToHashSet());

        // Categorized groups come back ordered by category id then type; where the uncategorized group sorts is the
        // provider's choice (the contract says so), so only its presence is asserted above.
        var categorized = totals.Where(t => t.CategoryId is not null).ToList();
        Assert.Equal(
            categorized.OrderBy(t => t.CategoryId).ThenBy(t => t.TransactionType, StringComparer.Ordinal).ToList(),
            categorized);
    }

    [PostgresFact]
    public async Task Should_SumPostedRowsDatedAfterTheDay_When_GettingPostedSumByPlaidAccount()
    {
        var ledger = await SeedMixedLedgerAsync();

        var afterFebFirst = await WithAccessorAsync(a => a.GetPostedSumByPlaidAccountAfterAsync(ledger.UserId, new DateTime(2026, 2, 1, 10, 0, 0)));
        var afterJanLast = await WithAccessorAsync(a => a.GetPostedSumByPlaidAccountAfterAsync(ledger.UserId, new DateTime(2026, 1, 31)));

        // Rows 3, 4 and 12; the transfer legs are dated on the day itself and pa-2 has nothing later that is posted.
        Assert.Equal(new Dictionary<string, decimal> { ["pa-1"] = 1965.00m }, afterFebFirst);
        Assert.Equal(new Dictionary<string, decimal> { ["pa-1"] = 1665.00m, ["pa-2"] = 300.00m }, afterJanLast);
    }

    [PostgresFact]
    public async Task Should_ReturnNewestIncomeInCategoriesUpToTheDay_When_GettingNewestIncome()
    {
        var ledger = await SeedMixedLedgerAsync();
        int[] categories = [ledger.Salary, ledger.Groceries, ledger.Salary];

        async Task<int[]> NewestAsync(DateTime onOrBefore, decimal? minAmount, int take) =>
            (await WithAccessorAsync(a => a.GetNewestIncomeAsync(ledger.UserId, categories, onOrBefore, minAmount, take)))
            .Select(t => t.Id).ToArray();

        // Row 12 is in Salary but is an Expense; row 4 is at 18:00 on the bound's day.
        Assert.Equal([ledger.FebSalary.Id, ledger.JanSalary.Id], await NewestAsync(new DateTime(2026, 2, 10), null, 5));
        Assert.Equal([ledger.JanSalary.Id], await NewestAsync(new DateTime(2026, 2, 9, 23, 0, 0), null, 5));
        Assert.Equal([ledger.FebSalary.Id], await NewestAsync(new DateTime(2026, 2, 10), 1800m, 5));
        Assert.Equal([ledger.FebSalary.Id], await NewestAsync(DateTime.MaxValue, null, 1));
    }

    [PostgresFact]
    public async Task Should_LeavePendingRowsWithoutAPlaidAccountOutOfPendingByAccount_When_GettingSummary()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var at = new DateTime(2026, 2, 12, 10, 0, 0);
        await fixture.TransactionsAsync(
            Row(account.Id, at, -4.00m, pending: true),
            Row(account.Id, at, -6.00m, plaidAccountId: "pa-1", pending: true));

        var summary = await WithAccessorAsync(a => a.GetSummaryAsync(user.Id));

        Assert.Equal(2, summary.UncategorizedCount);
        Assert.Equal([new PendingAccountTotal("pa-1", -6.00m)], summary.PendingByAccount);
    }

    [PostgresFact]
    public async Task Should_CountInflowAndOutflowDatedExactlyAtSince_When_GettingCategoryUsage()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var category = await fixture.CategoryAsync(user.Id, "Side gig", Income);
        var since = new DateTime(2026, 1, 20, 6, 30, 0);
        var justBefore = since.AddTicks(-TimeSpan.TicksPerMicrosecond);
        await fixture.TransactionsAsync(
            Row(account.Id, since, 75.50m, Income, category.Id),
            Row(account.Id, since, -3.25m, Expense, category.Id),
            Row(account.Id, justBefore, 1000.00m, Income, category.Id),
            Row(account.Id, justBefore, -500.00m, Expense, category.Id));

        var usage = await WithAccessorAsync(a => a.GetCategoryUsageAsync(user.Id, since));

        Assert.Equal([new CategoryUsage(category.Id, 4, 75.50m, -3.25m)], usage);
    }

    [PostgresFact]
    public async Task Should_StartAtMidnightAfterTheDayWhateverTheTimeOfDay_When_GettingPostedSumByPlaidAccount()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var nextMidnight = new DateTime(2026, 3, 6);
        await fixture.TransactionsAsync(
            Row(account.Id, nextMidnight.AddTicks(-TimeSpan.TicksPerMicrosecond), -1.00m, plaidAccountId: "pa-last-instant-of-day"),
            Row(account.Id, nextMidnight, -2.00m, plaidAccountId: "pa-next-midnight"),
            Row(account.Id, nextMidnight.AddHours(5), -4.00m, plaidAccountId: "pa-next-day-0500"),
            Row(account.Id, nextMidnight.AddHours(5), -8.00m, plaidAccountId: "pa-next-day-0500-pending", pending: true));

        // 10:00 on the 5th: the bound is the 6th at 00:00 inclusive, not 24 hours later (the 6th at 10:00).
        var sums = await WithAccessorAsync(a => a.GetPostedSumByPlaidAccountAfterAsync(user.Id, new DateTime(2026, 3, 5, 10, 0, 0)));

        Assert.Equal(
            new Dictionary<string, decimal> { ["pa-next-midnight"] = -2.00m, ["pa-next-day-0500"] = -4.00m },
            sums);
    }

    [PostgresFact]
    public async Task Should_NotReturnAnotherUsersIncome_When_ItsCategoryIdIsAmongTheRequestedCategories()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var salary = await fixture.CategoryAsync(user.Id, "Salary", Income);
        var other = await fixture.UserAsync();
        var otherAccount = await fixture.AccountAsync(other.Id);
        var otherSalary = await fixture.CategoryAsync(other.Id, "Salary", Income);
        var at = new DateTime(2026, 2, 10, 18, 0, 0);

        var mine = Row(account.Id, at, 2000.00m, Income, salary.Id);
        // Newer and larger than mine, once under their own category and once filed under mine.
        var theirs = Row(otherAccount.Id, at.AddHours(1), 9999.00m, Income, otherSalary.Id);
        var theirsInMyCategory = Row(otherAccount.Id, at.AddHours(2), 8888.00m, Income, salary.Id);
        await fixture.TransactionsAsync(mine, theirs, theirsInMyCategory);

        var newest = await WithAccessorAsync(a =>
            a.GetNewestIncomeAsync(user.Id, [salary.Id, otherSalary.Id], DateTime.MaxValue, null, 5));

        Assert.Equal([mine.Id], newest.Select(t => t.Id).ToArray());
    }

    [PostgresFact]
    public async Task Should_ExcludeNextMidnightAndIncludeTheMinimumAmount_When_GettingNewestIncomeAtTheBounds()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var salary = await fixture.CategoryAsync(user.Id, "Salary", Income);
        var nextMidnight = new DateTime(2026, 2, 11);

        var atNextMidnight = Row(account.Id, nextMidnight, 3000.00m, Income, salary.Id);
        var lastInstantOfDay = Row(account.Id, nextMidnight.AddTicks(-TimeSpan.TicksPerMicrosecond), 2000.00m, Income, salary.Id);
        var belowMinimum = Row(account.Id, nextMidnight.AddDays(-1), 1999.99m, Income, salary.Id);
        await fixture.TransactionsAsync(atNextMidnight, lastInstantOfDay, belowMinimum);

        async Task<int[]> NewestAsync(DateTime onOrBefore, decimal? minAmount) =>
            (await WithAccessorAsync(a => a.GetNewestIncomeAsync(user.Id, [salary.Id], onOrBefore, minAmount, 5)))
            .Select(t => t.Id).ToArray();

        Assert.Equal([lastInstantOfDay.Id, belowMinimum.Id], await NewestAsync(new DateTime(2026, 2, 10, 7, 0, 0), null));
        Assert.Equal([lastInstantOfDay.Id], await NewestAsync(new DateTime(2026, 2, 10), 2000.00m));
        Assert.Equal([atNextMidnight.Id, lastInstantOfDay.Id], await NewestAsync(nextMidnight, 2000.00m));
    }

    // ── Ordering of grouped results ─────────────────────────────────────────

    private sealed record ManyGroups(int UserId, int[] CategoryIds, string[] PlaidAccountIds);

    /// <summary>
    /// Sixteen categories and sixteen Plaid accounts, each with a pending Income and a pending Expense row, inserted
    /// highest key first and Income before Expense. The ordering tests read it with <see cref="Planner.HashedGrouping"/>:
    /// without an ORDER BY a HashAggregate returns groups in hash order, which for this many keys is not key order.
    /// </summary>
    private async Task<ManyGroups> SeedManyGroupsHighestKeyFirstAsync()
    {
        const int groups = 16;
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var categoryIds = new int[groups];
        for (var i = 0; i < groups; i++)
            categoryIds[i] = (await fixture.CategoryAsync(user.Id, $"Category {i:00}")).Id;
        var plaidAccountIds = Enumerable.Range(0, groups).Select(i => $"pa{i:00}").ToArray();

        var at = new DateTime(2026, 10, 1, 12, 0, 0);
        var rows = Enumerable.Range(0, groups).Reverse().SelectMany(i => new[]
        {
            Row(account.Id, at, 100m + i, Income, categoryIds[i], plaidAccountId: plaidAccountIds[i], pending: true),
            Row(account.Id, at, -1m - i, Expense, categoryIds[i], plaidAccountId: plaidAccountIds[i], pending: true)
        }).ToArray();
        await fixture.TransactionsAsync(rows);

        Assert.Equal(categoryIds.Order(), categoryIds);
        return new ManyGroups(user.Id, categoryIds, plaidAccountIds);
    }

    [PostgresFact]
    public async Task Should_OrderPendingTotalsByPlaidAccountId_When_GettingSummary()
    {
        var seeded = await SeedManyGroupsHighestKeyFirstAsync();

        var summary = await WithAccessorAsync(a => a.GetSummaryAsync(seeded.UserId), Planner.HashedGrouping);

        Assert.Equal(seeded.PlaidAccountIds, summary.PendingByAccount.Select(p => p.PlaidAccountId).ToArray());
    }

    [PostgresFact]
    public async Task Should_OrderUsageByCategoryId_When_GettingCategoryUsage()
    {
        var seeded = await SeedManyGroupsHighestKeyFirstAsync();

        var usage = await WithAccessorAsync(a => a.GetCategoryUsageAsync(seeded.UserId, DateTime.MinValue), Planner.HashedGrouping);

        Assert.Equal(seeded.CategoryIds, usage.Select(u => u.CategoryId).ToArray());
    }

    [PostgresFact]
    public async Task Should_OrderTotalsByCategoryIdThenType_When_GettingTotalsByCategoryAndType()
    {
        var seeded = await SeedManyGroupsHighestKeyFirstAsync();

        var totals = await WithAccessorAsync(a =>
            a.GetTotalsByCategoryAndTypeAsync(seeded.UserId, new DateTime(2026, 10, 1), new DateTime(2026, 10, 1)),
            Planner.HashedGrouping);

        // This pins the ORDER BY as a whole. The type tiebreak on its own cannot be pinned: once any ORDER BY on the
        // category is left, PostgreSQL groups by sorting on both keys, which orders the types as a side effect.
        var expected = seeded.CategoryIds.SelectMany(id => new[] { ((int?)id, Expense), ((int?)id, Income) }).ToArray();
        Assert.Equal(expected, totals.Select(t => (t.CategoryId, t.TransactionType)).ToArray());
    }

    [PostgresFact]
    public async Task Should_ReturnDistinctCategoryIdsIncludingNull_When_GettingCategoryIdsInUse()
    {
        var ledger = await SeedMixedLedgerAsync();

        var inUse = await WithAccessorAsync(a => a.GetCategoryIdsInUseAsync(ledger.UserId));

        Assert.Equal(3, inUse.Count);
        Assert.Equal(new HashSet<int?> { ledger.Groceries, ledger.Salary, null }, inUse.ToHashSet());
    }

    // ── GetLatestCategoryByPayeeAsync ───────────────────────────────────────

    [PostgresFact]
    public async Task Should_PickNewestCategorizedRowPerPayeeIgnoringCase_When_GettingLatestCategoryByPayee()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var older = await fixture.CategoryAsync(user.Id, "Older pick");
        var newer = await fixture.CategoryAsync(user.Id, "Newer pick");
        var other = await fixture.UserAsync();
        var otherAccount = await fixture.AccountAsync(other.Id);
        var otherCategory = await fixture.CategoryAsync(other.Id, "Theirs");

        var at = new DateTime(2026, 8, 1, 12, 0, 0);
        // Same instant: the row inserted second has the higher id and must win the tie.
        var tieFirst = Row(account.Id, at, categoryId: newer.Id, payee: "Amazon");
        var tieSecond = Row(account.Id, at, categoryId: older.Id, payee: "AMAZON");
        await fixture.TransactionsAsync(tieFirst);
        await fixture.TransactionsAsync(tieSecond);
        await fixture.TransactionsAsync(
            Row(account.Id, at.AddDays(-2), categoryId: older.Id, payee: "STARBUCKS"),
            Row(account.Id, at.AddDays(-1), categoryId: newer.Id, payee: "starbucks"),
            Row(account.Id, at.AddDays(1), payee: "Starbucks"),
            Row(account.Id, at, categoryId: older.Id, payee: "Not asked for"),
            Row(otherAccount.Id, at.AddDays(5), categoryId: otherCategory.Id, payee: "Starbucks"),
            Row(otherAccount.Id, at, categoryId: otherCategory.Id, payee: "Only theirs"));
        Assert.True(tieSecond.Id > tieFirst.Id);

        var latest = await WithAccessorAsync(a =>
            a.GetLatestCategoryByPayeeAsync(user.Id, ["StArBuCkS", "amazon", "Only theirs", "never seen", " "]));

        Assert.Equal(2, latest.Count);
        Assert.Equal(newer.Id, latest["Starbucks"]);
        Assert.Equal(older.Id, latest["Amazon"]);
    }

    [PostgresFact]
    public async Task Should_PickByDateNotById_When_TheNewestDatedRowWasInsertedFirst()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var older = await fixture.CategoryAsync(user.Id, "Older pick");
        var newer = await fixture.CategoryAsync(user.Id, "Newer pick");
        var at = new DateTime(2026, 8, 3, 12, 0, 0);

        var newestDated = Row(account.Id, at, categoryId: newer.Id, payee: "Costco");
        var backfilled = Row(account.Id, at.AddDays(-30), categoryId: older.Id, payee: "Costco");
        await fixture.TransactionsAsync(newestDated);
        await fixture.TransactionsAsync(backfilled);
        Assert.True(newestDated.Id < backfilled.Id);

        var latest = await WithAccessorAsync(a => a.GetLatestCategoryByPayeeAsync(user.Id, ["Costco"]));

        Assert.Equal(newer.Id, Assert.Single(latest).Value);
    }

    [PostgresFact]
    public async Task Should_BreakSameInstantTiesByHighestId_When_PhysicalRowOrderPointsEitherWay()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var loser = await fixture.CategoryAsync(user.Id, "Lower id");
        var winner = await fixture.CategoryAsync(user.Id, "Higher id");
        var at = new DateTime(2026, 8, 5, 12, 0, 0);

        // "Heap": inserted in id order, so the higher id is also the physically later row.
        var heapLow = Row(account.Id, at, categoryId: loser.Id, payee: "Heap");
        var heapHigh = Row(account.Id, at, categoryId: winner.Id, payee: "Heap");
        await fixture.TransactionsAsync(heapLow);
        await fixture.TransactionsAsync(heapHigh);

        // "Moved": the lower id is re-dated onto the tie afterwards. That UPDATE changes an indexed column, so Postgres
        // writes a new row version after the higher id's: physical order is now the reverse of id order. Whichever
        // physical end a tiebreak-less query favours, it gets one of the two payees wrong.
        var movedLow = Row(account.Id, at.AddDays(-9), categoryId: loser.Id, payee: "Moved");
        var movedHigh = Row(account.Id, at, categoryId: winner.Id, payee: "Moved");
        await fixture.TransactionsAsync(movedLow);
        await fixture.TransactionsAsync(movedHigh);
        await using (var context = fixture.CreateContext())
        {
            var updated = await context.Transactions
                .Where(t => t.Id == movedLow.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(t => t.OccurredAt, at));
            Assert.Equal(1, updated);
        }
        Assert.True(heapLow.Id < heapHigh.Id && movedLow.Id < movedHigh.Id);

        // Once with the default plan and once reading the heap directly, where nothing but the query's own
        // ORDER BY ... "Id" DESC can break the tie.
        foreach (var plannerSettings in new[] { null, Planner.NoIndexScans })
        {
            var latest = await WithAccessorAsync(
                a => a.GetLatestCategoryByPayeeAsync(user.Id, ["Heap", "Moved"]), plannerSettings);

            Assert.Equal(2, latest.Count);
            Assert.Equal(winner.Id, latest["Heap"]);
            Assert.Equal(winner.Id, latest["Moved"]);
        }
    }

    // ── Writes whose SQL or constraints are Postgres-specific ───────────────

    [PostgresFact]
    public async Task Should_UpdateOnlyTheOwnersRow_When_SettingNotes()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var other = await fixture.UserAsync();
        var otherAccount = await fixture.AccountAsync(other.Id);
        var mine = Row(account.Id, new DateTime(2026, 9, 1));
        var theirs = Row(otherAccount.Id, new DateTime(2026, 9, 1), notes: "untouched");
        await fixture.TransactionsAsync(mine, theirs);

        var ownRow = await WithAccessorAsync(a => a.SetNotesAsync(mine.Id, "lunch", user.Id));
        var foreignRow = await WithAccessorAsync(a => a.SetNotesAsync(theirs.Id, "hijacked", user.Id));

        await using var context = fixture.CreateContext();
        Assert.True(ownRow);
        Assert.False(foreignRow);
        Assert.Equal("lunch", (await context.Transactions.AsNoTracking().SingleAsync(t => t.Id == mine.Id)).Notes);
        Assert.Equal("untouched", (await context.Transactions.AsNoTracking().SingleAsync(t => t.Id == theirs.Id)).Notes);
    }

    [PostgresFact]
    public async Task Should_UpdateInPlaceAndKeepPlaidTransactionIdUnique_When_UpsertingTheSameImportTwice()
    {
        var user = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var plaidId = $"ptx-{Guid.NewGuid():N}";
        Transaction Import(decimal amount, bool pending) => new()
        {
            AccountId = account.Id,
            PlaidTransactionId = plaidId,
            PlaidAccountId = "pa-1",
            Amount = amount,
            OccurredAt = new DateTime(2026, 9, 2),
            TransactionType = Expense,
            Payee = "Diner",
            IsImported = true,
            IsPending = pending
        };

        var first = await WithAccessorAsync(a => a.UpsertImportedAsync([Import(-11.00m, pending: true)]));
        var second = await WithAccessorAsync(a => a.UpsertImportedAsync([Import(-12.50m, pending: false)]));

        Assert.Equal((1, 0), first);
        Assert.Equal((0, 1), second);
        await using var context = fixture.CreateContext();
        var stored = await context.Transactions.AsNoTracking().SingleAsync(t => t.PlaidTransactionId == plaidId);
        Assert.Equal(-12.50m, stored.Amount);
        Assert.False(stored.IsPending);

        // The backstop the read-then-write upsert leans on if two syncs ever overlap: the filtered unique index.
        context.Transactions.Add(Import(-1m, pending: false));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(duplicate.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("UQ_Transactions_PlaidTransactionId", postgres.ConstraintName);
    }
}
