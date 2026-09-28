using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
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

        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(transactions);
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
    public async Task GetAnalysis_CompletedMonth_ReportsPacingAsFinished()
    {
        var plan = Plan(isActive: true, monthsBeforeNow: 6);
        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Expense(1, -40m, PriorMonth.AddDays(9))]);
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
        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Expense(1, -40m, InsideMonth)]);
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
        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Expense(1, -40m, PriorMonth.AddDays(9))]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([plan]);

        var result = await BuildSut().GetAnalysisAsync(
            UserId, PriorMonth, PriorMonth.AddMonths(1).AddDays(-1), trendMonths: 3);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.PlanMonth);
    }

    [Fact]
    public async Task GetAnalysis_QueriesEachAccessorOnceAndPicksTheActivePlan()
    {
        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Expense(1, -40m, InsideMonth)]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Plan(isActive: false, "Stale"), Plan(isActive: true, "Current")]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths: 3);

        Assert.True(result.IsSuccess);
        Assert.Equal("Current", result.Value!.PlanMonth!.Plan.Name);
        Assert.Equal(40m, result.Value.PlanMonth.Pacing.ActualExpenses);
        _transactionAccessor.Verify(a => a.GetByUserIdAsync(UserId), Times.Once);
        _budgetPlanAccessor.Verify(a => a.GetByUserIdAsync(UserId), Times.Once);
    }

    [Fact]
    public async Task GetAnalysis_OlderPlanStillGovernsTheCurrentMonth()
    {
        // A plan does not expire with its month: one dated three months ago still governs today.
        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Expense(1, -55m, InsideMonth)]);
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
        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Expense(1, -10m, InsideMonth)]);
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
        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Expense(1, -10m, InsideMonth)]);
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
        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Expense(1, -25m, InsideMonth)]);
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
        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(
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
        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Expense(1, -10m, DateTime.UtcNow)]);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);

        var result = await BuildSut().GetAnalysisAsync(UserId, From, To, trendMonths: 6);

        // One category present, so the flat trend list is one row per requested month.
        Assert.Equal(6, result.Value!.MonthlyTrend.Count);
    }
}
