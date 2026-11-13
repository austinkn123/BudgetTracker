using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.Analysis;
using BudgetTracker.Server.Managers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BudgetTracker.Tests.Managers;

/// <summary>
/// The analysis computed by <see cref="BudgetAnalysisManager"/> (window spend aggregated in the database, raw rows only for
/// the analysed month and trend) must equal what the previous implementation produced (raw rows for the whole window,
/// spend summed in memory). The previous implementation is kept below, verbatim apart from inlining the old
/// <c>SummarizeSpend(IReadOnlyList&lt;Transaction&gt;)</c>, as the oracle. Both run against the same real
/// <see cref="TransactionAccessor"/> over the in-memory provider.
/// </summary>
public class BudgetAnalysisEquivalenceTests : IDisposable
{
    private const int UserId = 7;
    private const int OtherUserId = 8;

    private static readonly DateTime CurrentMonth = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    private static readonly DateTime PriorMonth = CurrentMonth.AddMonths(-2);
    private static readonly DateTime AllFrom = new(1970, 1, 1);
    private static readonly DateTime AllTo = new(9999, 12, 31);

    private readonly BudgetTrackerDbContext _context;
    private readonly BudgetAnalysisEngine _engine = new();
    private readonly Mock<IBudgetPlanAccessor> _plans = new();
    private int _nextId = 1;

    public BudgetAnalysisEquivalenceTests()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new BudgetTrackerDbContext(options);
        _context.Accounts.AddRange(
            new Account { Id = 1, UserId = UserId, Name = "Checking", AccountType = "depository" },
            new Account { Id = 2, UserId = UserId, Name = "Savings", AccountType = "depository" },
            new Account { Id = 3, UserId = OtherUserId, Name = "Someone else", AccountType = "depository" });
        _context.SaveChanges();

        // A plan in force for every month exercised, so PlanMonth is compared too.
        _plans.Setup(p => p.GetByUserIdAsync(UserId)).ReturnsAsync(
        [
            new BudgetPlan
            {
                Id = 1, UserId = UserId, Name = "Baseline", IsActive = true, PlanMonth = CurrentMonth.AddYears(-5),
                Entries =
                [
                    new BudgetPlanEntry { LineType = "Expense", Bucket = "Core", Cadence = "Monthly", Amount = 400m, MonthlyEquivalent = 400m, CategoryId = 1 },
                    new BudgetPlanEntry { LineType = "Expense", Bucket = "Buffer", Cadence = "Monthly", Amount = 100m, MonthlyEquivalent = 100m, CategoryId = 2 }
                ]
            }
        ]);
    }

    public void Dispose() => _context.Dispose();

    private void Add(string type, decimal amount, DateTime at, int? categoryId = 1, int accountId = 1) =>
        _context.Transactions.Add(new Transaction
        {
            Id = _nextId++,
            AccountId = accountId,
            CategoryId = categoryId,
            TransactionType = type,
            Amount = amount,
            OccurredAt = at,
            TransferAccountId = type == "Transfer" ? (accountId == 1 ? 2 : 1) : null
        });

    private async Task SaveAsync()
    {
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    /// <summary>A ledger with rows years old, in the trend, in the current month, and someone else's.</summary>
    private async Task SeedMixedLedgerAsync()
    {
        Add("Expense", -1200m, new DateTime(1971, 3, 4));
        Add("Expense", -60m, CurrentMonth.AddYears(-3), categoryId: 9);          // only ever used long ago
        Add("Income", 3000m, CurrentMonth.AddYears(-1), categoryId: 5);
        Add("Expense", -80m, PriorMonth.AddDays(3));
        Add("Expense", 30m, PriorMonth.AddDays(4));                              // refund
        Add("Transfer", -500m, PriorMonth.AddDays(5), categoryId: null);
        Add("Transfer", 500m, PriorMonth.AddDays(5), categoryId: null, accountId: 2);
        Add("Expense", -12.34m, PriorMonth.AddDays(6), categoryId: null);        // uncategorized
        Add("Income", 2500m, CurrentMonth.AddMonths(-1).AddDays(1), categoryId: 5);
        Add("Expense", -45.5m, CurrentMonth, categoryId: 2);
        Add("Expense", 5m, CurrentMonth, categoryId: 2);                         // refund
        Add("Expense", -7m, CurrentMonth, categoryId: null);
        Add("Expense", -999m, CurrentMonth, accountId: 3);                       // someone else's
        await SaveAsync();
    }

    private async Task AssertNewPathEqualsOldPath(DateTime from, DateTime to, int trendMonths = 3)
    {
        var accessor = new TransactionAccessor(_context);
        var expected = await LegacyGetAnalysisAsync(accessor, from, to, trendMonths);

        var actual = await new BudgetAnalysisManager(_engine, accessor, _plans.Object)
            .GetAnalysisAsync(UserId, from, to, trendMonths);

        Assert.True(actual.IsSuccess);
        AssertEquivalent(expected, actual.Value!);
    }

    // ── Scenarios ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_MatchTheOldPath_When_TheWindowIsTheAllRange()
    {
        await SeedMixedLedgerAsync();

        await AssertNewPathEqualsOldPath(AllFrom, AllTo);
        await AssertNewPathEqualsOldPath(AllFrom, AllTo, trendMonths: 24);
    }

    [Fact]
    public async Task Should_MatchTheOldPath_When_TheWindowIsAPastMonth()
    {
        await SeedMixedLedgerAsync();

        await AssertNewPathEqualsOldPath(PriorMonth, PriorMonth.AddMonths(1).AddDays(-1));
    }

    [Fact]
    public async Task Should_MatchTheOldPath_When_TheWindowStartsYearsBeforeTheTrend()
    {
        await SeedMixedLedgerAsync();

        await AssertNewPathEqualsOldPath(CurrentMonth.AddYears(-4), CurrentMonth.AddMonths(1).AddDays(-1), trendMonths: 6);
    }

    [Fact]
    public async Task Should_MatchTheOldPath_When_TheWindowHoldsRefunds()
    {
        Add("Expense", -80m, PriorMonth.AddDays(1));
        Add("Expense", 30m, PriorMonth.AddDays(2));
        Add("Expense", 50m, PriorMonth.AddDays(3), categoryId: 2);               // refund with no outflow: negative spend
        await SaveAsync();

        await AssertNewPathEqualsOldPath(PriorMonth, PriorMonth.AddDays(20));
    }

    [Fact]
    public async Task Should_MatchTheOldPath_When_TheWindowHoldsTransfers()
    {
        Add("Transfer", -500m, PriorMonth.AddDays(1), categoryId: null);
        Add("Transfer", 500m, PriorMonth.AddDays(1), categoryId: null, accountId: 2);
        Add("Transfer", -75m, PriorMonth.AddDays(2), categoryId: 1);             // categorized transfer: still not spend
        Add("Expense", -20m, PriorMonth.AddDays(2));
        await SaveAsync();

        await AssertNewPathEqualsOldPath(AllFrom, AllTo);
        await AssertNewPathEqualsOldPath(PriorMonth, PriorMonth.AddDays(5));
    }

    [Fact]
    public async Task Should_MatchTheOldPath_When_TheWindowHoldsUncategorizedRows()
    {
        Add("Expense", -12m, PriorMonth.AddDays(1), categoryId: null);
        Add("Expense", 2m, PriorMonth.AddDays(2), categoryId: null);
        Add("Income", 40m, PriorMonth.AddDays(2), categoryId: null);
        Add("Expense", -5m, PriorMonth.AddDays(2), categoryId: 3);
        await SaveAsync();

        await AssertNewPathEqualsOldPath(PriorMonth, PriorMonth.AddDays(5));
    }

    [Fact]
    public async Task Should_MatchTheOldPath_When_ARowFallsAt2359OnTheLastDay()
    {
        var to = PriorMonth.AddDays(9);
        Add("Expense", -1m, PriorMonth.AddMinutes(-1));                          // just before from
        Add("Expense", -2m, PriorMonth);                                         // first instant of from
        Add("Expense", -3m, to.AddHours(23).AddMinutes(59));                     // 23:59 on to
        Add("Expense", -4m, to.AddDays(1));                                      // midnight after to
        await SaveAsync();

        await AssertNewPathEqualsOldPath(PriorMonth, to);
        var window = (await new BudgetAnalysisManager(_engine, new TransactionAccessor(_context), _plans.Object)
            .GetAnalysisAsync(UserId, PriorMonth, to, 3)).Value!.WindowSpend;
        Assert.Equal(new CategorySpend(1, 5m), Assert.Single(window));
    }

    [Fact]
    public async Task Should_MatchTheOldPath_When_TheLedgerIsEmpty()
    {
        await AssertNewPathEqualsOldPath(AllFrom, AllTo);
        await AssertNewPathEqualsOldPath(PriorMonth, PriorMonth.AddMonths(1).AddDays(-1));
    }

    // ── Comparison ──────────────────────────────────────────────────────────

    /// <summary>
    /// Lists are compared order-insensitively: <see cref="BudgetAnalysis"/> promises no ordering, and the old path's order
    /// was an artefact of in-memory grouping over newest-first rows.
    /// </summary>
    private static void AssertEquivalent(BudgetAnalysis expected, BudgetAnalysis actual)
    {
        Assert.Equal(
            expected.WindowSpend.OrderBy(s => s.CategoryId),
            actual.WindowSpend.OrderBy(s => s.CategoryId));
        Assert.Equal(
            expected.MonthlyTrend.OrderBy(p => p.CategoryId).ThenBy(p => p.Month),
            actual.MonthlyTrend.OrderBy(p => p.CategoryId).ThenBy(p => p.Month));

        Assert.Equal(expected.PlanMonth is null, actual.PlanMonth is null);
        if (expected.PlanMonth is not { } e || actual.PlanMonth is not { } a) return;

        Assert.Equal((e.Plan, e.AnalyzedMonth, e.Pacing, e.Income, e.Expenses), (a.Plan, a.AnalyzedMonth, a.Pacing, a.Income, a.Expenses));
        Assert.Equal(e.ByCategory.OrderBy(c => c.CategoryId), a.ByCategory.OrderBy(c => c.CategoryId));
        Assert.Equal(e.ByBucket.OrderBy(b => b.Bucket), a.ByBucket.OrderBy(b => b.Bucket));
    }

    // ── Oracle: the implementation as of 6a81a89 ───────────────────────────

    private async Task<BudgetAnalysis> LegacyGetAnalysisAsync(ITransactionAccessor transactionAccessor, DateTime from, DateTime to, int trendMonths)
    {
        var engine = _engine;
        var now = DateTime.UtcNow;

        var lastDay = to < now ? to : now;
        var requestedMonth = new DateTime(lastDay.Year, lastDay.Month, 1);
        var lastInstantOfRequestedMonth = requestedMonth.AddMonths(1).AddDays(-1);
        var asOf = lastInstantOfRequestedMonth < now ? lastInstantOfRequestedMonth : now;
        var analyzedMonth = new DateTime(asOf.Year, asOf.Month, 1);

        var trendStart = engine.TrendStart(asOf, trendMonths);
        var analyzedMonthEnd = analyzedMonth.AddMonths(1).AddDays(-1);
        var fetch = new TransactionFilter(
            From: from.Date < trendStart ? from.Date : trendStart,
            To: to.Date > analyzedMonthEnd ? to.Date : analyzedMonthEnd);

        var transactions = (await transactionAccessor.GetFilteredAsync(UserId, fetch)).ToList();
        var categoryIdsInUse = await transactionAccessor.GetCategoryIdsInUseAsync(UserId);
        var budgetPlans = await _plans.Object.GetByUserIdAsync(UserId);

        var governingPlan = budgetPlans
            .Where(p => p.IsActive && p.PlanMonth <= analyzedMonth)
            .OrderByDescending(p => p.PlanMonth)
            .FirstOrDefault();

        var windowTransactions = transactions
            .Where(t => t.OccurredAt >= from.Date && t.OccurredAt.Date <= to.Date)
            .ToList();

        return new BudgetAnalysis(
            governingPlan is null ? null : engine.AnalyzeMonth(transactions, governingPlan, asOf),
            LegacySummarizeSpend(windowTransactions),
            engine.SummarizeByMonth(transactions, asOf, trendMonths, categoryIdsInUse));
    }

    private static IReadOnlyList<CategorySpend> LegacySummarizeSpend(IReadOnlyList<Transaction> transactions) =>
        transactions
            .Where(t => t.TransactionType == "Expense")
            .GroupBy(t => t.CategoryId)
            .Select(g => new CategorySpend(g.Key, g.Sum(t => -t.Amount)))
            .ToList();
}
