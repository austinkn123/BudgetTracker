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
    private static readonly DateTime From = new(2026, 6, 1);
    private static readonly DateTime To = new(2026, 6, 30);

    private static BudgetPlan Plan(bool isActive, string name = "Plan") => new()
    {
        Id = 3,
        UserId = UserId,
        Name = name,
        PlanMonth = new DateTime(2026, 6, 1),
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

    [Fact]
    public async Task GetAnalysis_QueriesEachAccessorOnceAndPicksTheActivePlan()
    {
        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Expense(1, -40m, new DateTime(2026, 6, 10))]);
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
    public async Task GetAnalysis_NoActivePlan_StillReturnsWindowSpend()
    {
        _transactionAccessor.Setup(a => a.GetByUserIdAsync(UserId))
            .ReturnsAsync([Expense(1, -25m, new DateTime(2026, 6, 10))]);
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
            Expense(1, -1m, new DateTime(2026, 5, 31, 23, 59, 0)),   // just before the window
            Expense(2, -2m, From),                                    // first instant of `from`
            Expense(3, -3m, To.AddHours(23).AddMinutes(59)),          // last instant of `to`
            Expense(4, -4m, new DateTime(2026, 7, 1))                 // just after
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
