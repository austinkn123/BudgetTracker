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

    private async Task<T> WithAccessorAsync<T>(Func<TransactionAccessor, Task<T>> act)
    {
        await using var context = fixture.CreateContext();
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
