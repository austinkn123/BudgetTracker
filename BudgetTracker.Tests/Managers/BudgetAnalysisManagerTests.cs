using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.Analysis;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class BudgetAnalysisManagerTests
{
    private readonly Mock<ITransactionAccessor> _transactionAccessor = new(MockBehavior.Strict);
    private readonly Mock<IBudgetPlanAccessor> _budgetPlanAccessor = new(MockBehavior.Strict);
    private readonly BudgetAnalysisEngine _engine = new();

    private BudgetAnalysisManager BuildSut() =>
        new(_engine, _transactionAccessor.Object, _budgetPlanAccessor.Object);

    private const int UserId = 7;

    // The manager reads DateTime.UtcNow to decide which month it is analysing, so the fixtures
    // are anchored to the real current month rather than a fixed date. Day 10 is safe in every
    // month, including February.
    private static readonly DateTime CurrentMonth =
        new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

    private static readonly DateTime From = CurrentMonth;
    private static readonly DateTime To = CurrentMonth.AddMonths(1).AddDays(-1);
    private static readonly DateTime InsideMonth = CurrentMonth.AddDays(9);

    private static BudgetPlan Plan(
        bool isActive,
        string name = "Plan",
        int monthsBeforeNow = 0,
        int id = 3) => new()
    {
        Id = id,
        UserId = UserId,
        Name = name,
        PlanMonth = CurrentMonth.AddMonths(-monthsBeforeNow),
        IsActive = isActive,
        Entries = [new BudgetPlanEntry
        {
            LineType = "Expense", Bucket = "Core", Cadence = "Monthly",
            Amount = 100m, MonthlyEquivalent = 100m, CategoryId = 1
        }]
    };

    private readonly List<TransactionFilter> _filters = [];
    private readonly List<(DateTime From, DateTime To)> _totalWindows = [];

    /// <summary>
    /// Stands in for the ledger: the filtered read honours From/To the way the accessor does (whole days, To inclusive),
    /// so a test fails if the manager stops fetching rows it needs.
    /// </summary>
    private void StubLedger(IEnumerable<Transaction> ledger)
    {
        var rows = ledger.ToList();
        _transactionAccessor.Setup(a => a.GetFilteredAsync(UserId, It.IsAny<TransactionFilter>()))
            .Callback<int, TransactionFilter>((_, f) => _filters.Add(f))
            .ReturnsAsync((int _, TransactionFilter f) => rows
                .Where(t => f.From is not DateTime from || t.OccurredAt >= from.Date)
                .Where(t => f.To is not DateTime to || t.OccurredAt.Date <= to.Date)
                .OrderByDescending(t => t.OccurredAt)
                .ToList());
        _transactionAccessor.Setup(a => a.GetCategoryIdsInUseAsync(UserId))
            .ReturnsAsync(rows.Select(t => t.CategoryId).Distinct().ToList());
        // Same whole-day bounds as the filtered read, aggregated the way the SQL GROUP BY does.
        _transactionAccessor.Setup(a => a.GetTotalsByCategoryAndTypeAsync(UserId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback<int, DateTime, DateTime>((_, from, to) => _totalWindows.Add((from, to)))
            .ReturnsAsync((int _, DateTime from, DateTime to) => rows
                .Where(t => t.OccurredAt >= from.Date && t.OccurredAt.Date <= to.Date)
                .GroupBy(t => (t.CategoryId, t.TransactionType))
                .Select(g => new CategoryTypeTotal(g.Key.CategoryId, g.Key.TransactionType, g.Sum(t => t.Amount)))
                .ToList());
    }

    private static Transaction Expense(int id, decimal signed, DateTime occurredAt) => new()
    {
        Id = id,
        AccountId = 1,
        CategoryId = 1,
        TransactionType = "Expense",
        Amount = signed,
        OccurredAt = occurredAt
    };

    // ── Per-month analysis (the window drives the analysed month) ───────────

    private static readonly DateTime PriorMonth = CurrentMonth.AddMonths(-2);

    [Fact]
    public async Task GetAnalysis_WindowInAPastMonth_AnalysesThatMonthNotToday()
    {
        var plan = Plan(isActive: true, monthsBeforeNow: 6);
        // One expense in the past month, one in the current month. Only the first should count.
        var transactions = new List<Transaction>
        {
            Expense(1, -40m, PriorMonth.AddDays(9)),
            Expense(2, -25m, CurrentMonth.AddDays(9)),
        };

        StubLedger(transactions);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([plan]);

        var priorFrom = PriorMonth;
        var priorTo = PriorMonth.AddMonths(1).AddDays(-1);

        var result = await BuildSut().GetAnalysisAsync(UserId, priorFrom, priorTo, trendMonths: 3);

        Assert.True(result.IsSuccess);
        var planMonth = result.Value!.PlanMonth;
        Assert.NotNull(planMonth);
        Assert.Equal(PriorMonth, planMonth!.AnalyzedMonth);
        Assert.Equal(40m, planMonth.Pacing.ActualExpenses);
    }

    [Fact]
    public async Task GetAnalysis_OpenEndedAllTimeWindow_AnalysesTheCurrentMonth()
    {
        // The dashboard's ALL range sends 9999-12-31; stepping a month past it used to overflow DateTime.
        StubLedger([Expense(1, -40m, CurrentMonth.AddDays(1))]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([Plan(isActive: true, monthsBeforeNow: 6)]);

        var result = await BuildSut().GetAnalysisAsync(UserId, new DateTime(1970, 1, 1), new DateTime(9999, 12, 31), trendMonths: 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(CurrentMonth, result.Value!.PlanMonth!.AnalyzedMonth);
    }

    [Fact]
    public async Task GetAnalysis_CompletedMonth_ReportsPacingAsFinished()
    {
        var plan = Plan(isActive: true, monthsBeforeNow: 6);
        StubLedger([Expense(1, -40m, PriorMonth.AddDays(9))]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([plan]);

        var result = await BuildSut().GetAnalysisAsync(
            UserId, PriorMonth, PriorMonth.AddMonths(1).AddDays(-1), trendMonths: 3);

        var pacing = result.Value!.PlanMonth!.Pacing;
        // A month that has already ended is fully elapsed, so no days remain to spread spend over.
        Assert.Equal(pacing.DaysInMonth, pacing.DaysElapsed);
        Assert.Equal(0, pacing.PerDiemToStay);
    }

    [Fact]
    public async Task GetAnalysis_CurrentMonth_StillClampsToToday()
    {
        var plan = Plan(isActive: true);
        StubLedger([Expense(1, -40m, InsideMonth)]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([plan]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths: 3);

        var pacing = result.Value!.PlanMonth!.Pacing;
        // Asking for the whole current month must not pretend the month is over.
        Assert.Equal(DateTime.UtcNow.Day, pacing.DaysElapsed);
    }

    [Fact]
    public async Task GetAnalysis_PlanNotYetInEffectForThatMonth_ReturnsNoPlanMonth()
    {
        // The plan takes effect next month, so a window two months back has nothing governing it.
        var plan = Plan(isActive: true, monthsBeforeNow: -1);
        StubLedger([Expense(1, -40m, PriorMonth.AddDays(9))]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([plan]);

        var result = await BuildSut().GetAnalysisAsync(
            UserId, PriorMonth, PriorMonth.AddMonths(1).AddDays(-1), trendMonths: 3);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.PlanMonth);
    }

    [Fact]
    public async Task GetAnalysis_QueriesEachAccessorOnceAndPicksTheActivePlan()
    {
        StubLedger([Expense(1, -40m, InsideMonth)]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Plan(isActive: false, "Stale"), Plan(isActive: true, "Current")]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths: 3);

        Assert.True(result.IsSuccess);
        Assert.Equal("Current", result.Value!.PlanMonth!.Plan.Name);
        Assert.Equal(40m, result.Value.PlanMonth.Pacing.ActualExpenses);
        _transactionAccessor.Verify(a => a.GetFilteredAsync(UserId, It.IsAny<TransactionFilter>()), Times.Once);
        _transactionAccessor.Verify(a => a.GetTotalsByCategoryAndTypeAsync(UserId, From, To), Times.Once);
        _budgetPlanAccessor.Verify(a => a.GetByUserIdAsync(UserId), Times.Once);
    }

    [Fact]
    public async Task GetAnalysis_OlderPlanStillGovernsTheCurrentMonth()
    {
        // A plan does not expire with its month: one dated three months ago still governs today.
        StubLedger([Expense(1, -55m, InsideMonth)]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Plan(isActive: true, "Baseline", monthsBeforeNow: 3)]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths: 3);

        var performance = result.Value!.PlanMonth!;
        Assert.Equal("Baseline", performance.Plan.Name);
        Assert.Equal(CurrentMonth, performance.AnalyzedMonth);                  // reports THIS month
        Assert.Equal(CurrentMonth.AddMonths(-3), performance.Plan.PlanMonth);   // effective from
        Assert.Equal(55m, performance.Pacing.ActualExpenses);
    }

    [Fact]
    public async Task GetAnalysis_PicksTheNewestPlanThatHasTakenEffect()
    {
        StubLedger([Expense(1, -10m, InsideMonth)]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(
        [
            Plan(isActive: true, "Oldest", monthsBeforeNow: 6, id: 1),
            Plan(isActive: true, "Newest", monthsBeforeNow: 1, id: 2),
            Plan(isActive: true, "Middle", monthsBeforeNow: 3, id: 3)
        ]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths: 3);

        Assert.Equal("Newest", result.Value!.PlanMonth!.Plan.Name);
    }

    [Fact]
    public async Task GetAnalysis_IgnoresAPlanThatHasNotTakenEffectYet()
    {
        // A plan dated next month must not govern this one.
        StubLedger([Expense(1, -10m, InsideMonth)]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(
        [
            Plan(isActive: true, "InForce", monthsBeforeNow: 1, id: 1),
            Plan(isActive: true, "Future", monthsBeforeNow: -1, id: 2)
        ]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths: 3);

        Assert.Equal("InForce", result.Value!.PlanMonth!.Plan.Name);
    }

    [Fact]
    public async Task GetAnalysis_NoActivePlan_StillReturnsWindowSpend()
    {
        StubLedger([Expense(1, -25m, InsideMonth)]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Plan(isActive: false)]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths: 3);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.PlanMonth);
        Assert.Equal(25m, Assert.Single(result.Value.WindowSpend).Amount);
    }

    [Fact]
    public async Task GetAnalysis_WindowIsInclusiveOfBothEndDays()
    {
        StubLedger(
        [
            Expense(1, -1m, From.AddMinutes(-1)),              // just before the window
            Expense(2, -2m, From),                             // first instant of `from`
            Expense(3, -3m, To.AddHours(23).AddMinutes(59)),   // last instant of `to`
            Expense(4, -4m, To.AddDays(1))                     // just after
        ]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths: 3);

        // Only transactions 2 and 3 fall inside, totalling 5.
        Assert.Equal(5m, Assert.Single(result.Value!.WindowSpend).Amount);
    }

    [Fact]
    public async Task GetAnalysis_TrendMonthsIsPassedThroughToTheEngine()
    {
        StubLedger([Expense(1, -10m, DateTime.UtcNow)]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths: 6);

        // One category present, so the flat trend list is one row per requested month.
        Assert.Equal(6, result.Value!.MonthlyTrend.Count);
    }

    // ── Bounded reads ───────────────────────────────────────────────────────

    [Fact]
    public async Task Should_FetchRawRowsFromTheTrendStart_ThroughTheAnalysedMonthEnd()
    {
        StubLedger([]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);
        var windowFrom = PriorMonth.AddDays(4);
        var windowTo = PriorMonth.AddDays(9);

        await BuildSut().GetAnalysisAsync(UserId, windowFrom, windowTo, trendMonths: 6);

        // Analysed month is PriorMonth (from `to`); its trend reaches back five more months and on to its last day.
        var filter = Assert.Single(_filters);
        Assert.Equal(new TransactionFilter(From: PriorMonth.AddMonths(-5), To: PriorMonth.AddMonths(1).AddDays(-1)), filter);
    }

    [Fact]
    public async Task Should_FetchRawRowsOnlyForTheTrendAndAnalysedMonth_When_TheWindowIsAllTime()
    {
        StubLedger([]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);

        await BuildSut().GetAnalysisAsync(UserId, new DateTime(1970, 1, 1), new DateTime(9999, 12, 31), trendMonths: 3);

        // The ALL range no longer drags the whole ledger in: rows cover only the trend through the current month's end.
        Assert.Equal(
            new TransactionFilter(From: CurrentMonth.AddMonths(-2), To: CurrentMonth.AddMonths(1).AddDays(-1)),
            Assert.Single(_filters));
    }

    [Fact]
    public async Task Should_AggregateWindowSpendInTheDatabase_OverExactlyTheRequestedWindow()
    {
        StubLedger([]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);

        await BuildSut().GetAnalysisAsync(UserId, new DateTime(1970, 1, 1), new DateTime(9999, 12, 31), trendMonths: 3);

        Assert.Equal((new DateTime(1970, 1, 1), new DateTime(9999, 12, 31)), Assert.Single(_totalWindows));
    }

    [Fact]
    public async Task Should_FetchOnlyTheTrendSpan_When_TheWindowStartsBeforeIt()
    {
        StubLedger([]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);
        var windowFrom = CurrentMonth.AddYears(-3);

        await BuildSut().GetAnalysisAsync(UserId, windowFrom, To, trendMonths: 3);

        Assert.Equal(new TransactionFilter(From: CurrentMonth.AddMonths(-2), To: To), Assert.Single(_filters));
        Assert.Equal((windowFrom, To), Assert.Single(_totalWindows));
    }

    [Fact]
    public async Task Should_NetRefundsAndIgnoreTransfersAndIncome_InWindowSpend()
    {
        var refund = Expense(2, 30m, InsideMonth);
        var transfer = Expense(3, -500m, InsideMonth);
        transfer.TransactionType = "Transfer";
        var income = Expense(4, 900m, InsideMonth);
        income.TransactionType = "Income";
        StubLedger([Expense(1, -80m, InsideMonth), refund, transfer, income]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths: 3);

        Assert.Equal(new CategorySpend(1, 50m), Assert.Single(result.Value!.WindowSpend));
    }

    [Fact]
    public async Task Should_KeepZeroSeededTrendSeries_ForCategoriesOnlyUsedOutsideTheFetchedRange()
    {
        // Category 9 was last used two years ago: it is not fetched, yet its zeroed series must still appear.
        var old = Expense(1, -40m, CurrentMonth.AddYears(-2));
        old.CategoryId = 9;
        StubLedger([old, Expense(2, -25m, InsideMonth)]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths: 3);

        var series = result.Value!.MonthlyTrend.Where(p => p.CategoryId == 9).ToList();
        Assert.Equal(3, series.Count);
        Assert.All(series, p => Assert.Equal((0m, 0m), (p.Income, p.Expenses)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(25)]
    public async Task Should_ReturnError_When_TrendMonthsIsOutOfRange(int trendMonths)
    {
        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths);

        Assert.False(result.IsSuccess);
        Assert.Contains("24", result.Error);
        _transactionAccessor.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    public async Task Should_AcceptTrendMonths_AtTheBoundaries(int trendMonths)
    {
        StubLedger([Expense(1, -10m, InsideMonth)]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths);

        Assert.True(result.IsSuccess);
        Assert.Equal(trendMonths, result.Value!.MonthlyTrend.Count);
    }
}
