using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.Analysis;

namespace BudgetTracker.Tests.Engines;

public class BudgetAnalysisEngineTests
{
    private readonly BudgetAnalysisEngine _sut = new();

    // June 2026 is a 30-day month, which keeps the mid-month pacing arithmetic readable.
    private static readonly DateTime PlanMonth = new(2026, 6, 1);
    private static readonly DateTime MidJune = new(2026, 6, 15);

    private static Transaction Expense(int id, decimal signedAmount, DateTime occurredAt, int? categoryId = 1) => new()
    {
        Id = id,
        AccountId = 1,
        CategoryId = categoryId,
        TransactionType = "Expense",
        Amount = signedAmount,
        OccurredAt = occurredAt
    };

    private static Transaction Income(int id, decimal amount, DateTime occurredAt, int? categoryId = 1) => new()
    {
        Id = id,
        AccountId = 1,
        CategoryId = categoryId,
        TransactionType = "Income",
        Amount = amount,
        OccurredAt = occurredAt
    };

    private static BudgetPlanEntry ExpenseLine(int? categoryId, decimal monthly, string bucket = "Core") => new()
    {
        CategoryId = categoryId,
        LineType = "Expense",
        Bucket = bucket,
        Cadence = "Monthly",
        Amount = monthly,
        MonthlyEquivalent = monthly
    };

    private static BudgetPlan Plan(params BudgetPlanEntry[] entries) => new()
    {
        Id = 10,
        UserId = 1,
        Name = "June Plan",
        PlanMonth = PlanMonth,
        IsActive = true,
        Entries = entries
    };

    // ---- AnalyzePlanMonth ----

    [Fact]
    public void SignedExpense_AggregatesAsPositiveMagnitude()
    {
        var result = _sut.AnalyzePlanMonth([Expense(1, -50m, MidJune)], Plan(ExpenseLine(1, 100m)), MidJune);

        Assert.Equal(50m, result.Pacing.ActualExpenses);
        Assert.Equal(50m, result.Expenses);
        var category = Assert.Single(result.ByCategory);
        Assert.Equal((1, 100m, 50m), (category.CategoryId, category.Planned, category.Actual));
    }

    [Fact]
    public void PlanMonthScope_ExcludesAdjacentMonths()
    {
        var result = _sut.AnalyzePlanMonth(
            [
                Expense(1, -10m, new DateTime(2026, 5, 31)),
                Expense(2, -20m, MidJune),
                Expense(3, -40m, new DateTime(2026, 7, 1))
            ],
            Plan(ExpenseLine(1, 100m)), MidJune);

        Assert.Equal(20m, result.Pacing.ActualExpenses);
    }

    [Fact]
    public void Income_SumsSignedAndIsSeparateFromExpenses()
    {
        var result = _sut.AnalyzePlanMonth(
            [Income(1, 1000m, MidJune), Expense(2, -250m, MidJune)],
            Plan(ExpenseLine(1, 100m)), MidJune);

        Assert.Equal(1000m, result.Income);
        Assert.Equal(250m, result.Expenses);
    }

    [Theory]
    [InlineData(2026, 5, 1, 0)]     // before the plan month
    [InlineData(2026, 6, 15, 15)]   // during
    [InlineData(2026, 8, 1, 30)]    // after
    public void DaysElapsed_ClampsAroundPlanMonth(int year, int month, int day, int expected)
    {
        var result = _sut.AnalyzePlanMonth([], Plan(ExpenseLine(1, 100m)), new DateTime(year, month, day));

        Assert.Equal(expected, result.Pacing.DaysElapsed);
        Assert.Equal(30, result.Pacing.DaysInMonth);
    }

    [Theory]
    [InlineData(-45, PacingStatus.Ahead)]      // spentPct .45 vs daysPct .50 -> delta exactly -0.05
    [InlineData(-50, PacingStatus.OnTrack)]    // delta 0
    [InlineData(-55, PacingStatus.Behind)]     // delta exactly +0.05
    public void Status_ThresholdsAreInclusiveAtFivePercent(int signedSpend, PacingStatus expected)
    {
        var result = _sut.AnalyzePlanMonth([Expense(1, signedSpend, MidJune)], Plan(ExpenseLine(1, 100m)), MidJune);

        Assert.Equal(expected, result.Pacing.Status);
    }

    [Fact]
    public void ProjectionAndPerDiem_UseLinearRunRate()
    {
        var pacing = _sut.AnalyzePlanMonth([Expense(1, -45m, MidJune)], Plan(ExpenseLine(1, 100m)), MidJune).Pacing;

        Assert.Equal(90m, pacing.ProjectedEnd);        // 45 over 15 days, extrapolated to 30
        Assert.Equal(55m, pacing.Remaining);
        Assert.Equal(55m / 15, pacing.PerDiemToStay);
        Assert.Equal(0.45m, pacing.SpentPct);
    }

    [Fact]
    public void BeforeMonthStarts_ProjectionFallsBackToActualAndPerDiemUsesWholeMonth()
    {
        var pacing = _sut.AnalyzePlanMonth(
            [Expense(1, -20m, MidJune)], Plan(ExpenseLine(1, 100m)), new DateTime(2026, 5, 1)).Pacing;

        Assert.Equal(0, pacing.DaysElapsed);
        Assert.Equal(20m, pacing.ProjectedEnd);   // no run rate yet, so no extrapolation
        Assert.Equal(80m / 30, pacing.PerDiemToStay);
    }

    [Fact]
    public void AfterMonthEnds_PerDiemIsZero()
    {
        var pacing = _sut.AnalyzePlanMonth(
            [Expense(1, -20m, MidJune)], Plan(ExpenseLine(1, 100m)), new DateTime(2026, 8, 1)).Pacing;

        Assert.Equal(0m, pacing.PerDiemToStay);
    }

    [Fact]
    public void NothingPlanned_SpentPctIsZeroRatherThanDivideByZero()
    {
        var pacing = _sut.AnalyzePlanMonth([Expense(1, -30m, MidJune)], Plan(), MidJune).Pacing;

        Assert.Equal(0m, pacing.SpentPct);
        Assert.Equal(30m, pacing.ActualExpenses);
    }

    [Fact]
    public void Buckets_UnplannedCategoryFallsBackToBuffer()
    {
        var result = _sut.AnalyzePlanMonth(
            [
                Expense(1, -30m, MidJune, categoryId: 1),   // planned as Core
                Expense(2, -40m, MidJune, categoryId: 2)    // absent from the plan
            ],
            Plan(ExpenseLine(1, 100m, "Core")), MidJune);

        var core = result.ByBucket.Single(b => b.Bucket == "Core");
        var buffer = result.ByBucket.Single(b => b.Bucket == "Buffer");
        Assert.Equal((100m, 30m), (core.Planned, core.Actual));
        Assert.Equal((0m, 40m), (buffer.Planned, buffer.Actual));
    }

    [Fact]
    public void Buckets_AlwaysReturnsCoreAndBufferEvenWhenEmpty()
    {
        var result = _sut.AnalyzePlanMonth([], Plan(), MidJune);

        Assert.Equal(["Core", "Buffer"], result.ByBucket.Select(b => b.Bucket));
    }

    [Fact]
    public void UncategorizedSpend_UsesNullCategoryIdAndLandsInBuffer()
    {
        var result = _sut.AnalyzePlanMonth(
            [Expense(1, -35m, MidJune, categoryId: null)], Plan(ExpenseLine(1, 100m)), MidJune);

        Assert.Contains(result.ByCategory, c => c.CategoryId is null && c.Actual == 35m);
        Assert.Equal(35m, result.ByBucket.Single(b => b.Bucket == "Buffer").Actual);
    }

    [Fact]
    public void ByCategory_UnionsPlannedAndActualAndExposesOverBy()
    {
        var result = _sut.AnalyzePlanMonth(
            [Expense(1, -30m, MidJune, categoryId: 2)],           // actual only
            Plan(ExpenseLine(1, 100m), ExpenseLine(2, 10m)),      // category 1 planned only
            MidJune);

        Assert.Equal(2, result.ByCategory.Count);
        Assert.Equal(-100m, result.ByCategory.Single(c => c.CategoryId == 1).OverBy);
        Assert.Equal(20m, result.ByCategory.Single(c => c.CategoryId == 2).OverBy);
    }

    [Fact]
    public void ByCategory_IsNotTruncatedOrOrdered()
    {
        // Presentation concerns live in the client; the engine returns everything.
        var transactions = Enumerable.Range(1, 12)
            .Select(i => Expense(i, -(i * 10), MidJune, categoryId: i))
            .ToList();

        var result = _sut.AnalyzePlanMonth(transactions, Plan(), MidJune);

        Assert.Equal(12, result.ByCategory.Count);
    }

    // ---- SummarizeSpend ----

    [Fact]
    public void SummarizeSpend_TotalsExpenseMagnitudePerCategoryAndIgnoresIncome()
    {
        var spend = _sut.SummarizeSpend(
        [
            Expense(1, -10m, MidJune, categoryId: 1),
            Expense(2, -15m, MidJune, categoryId: 1),
            Expense(3, -40m, MidJune, categoryId: 2),
            Income(4, 500m, MidJune, categoryId: 1)
        ]);

        Assert.Equal(2, spend.Count);
        Assert.Equal(25m, spend.Single(s => s.CategoryId == 1).Amount);
        Assert.Equal(40m, spend.Single(s => s.CategoryId == 2).Amount);
    }

    [Fact]
    public void SummarizeSpend_EmptyInputReturnsEmpty() => Assert.Empty(_sut.SummarizeSpend([]));

    // ---- SummarizeByMonth ----

    [Fact]
    public void SummarizeByMonth_CoversRequestedWindowAndZeroSeedsGaps()
    {
        var trend = _sut.SummarizeByMonth(
            [
                Expense(1, -10m, new DateTime(2026, 3, 10)),   // older than the window
                Expense(2, -20m, new DateTime(2026, 4, 10)),
                Expense(3, -30m, MidJune)
            ],
            MidJune, months: 3);

        var series = trend.Where(t => t.CategoryId == 1).OrderBy(t => t.Month).ToList();
        Assert.Equal(
            [new DateTime(2026, 4, 1), new DateTime(2026, 5, 1), new DateTime(2026, 6, 1)],
            series.Select(s => s.Month));
        Assert.Equal([20m, 0m, 30m], series.Select(s => s.Expenses));
    }

    [Fact]
    public void SummarizeByMonth_SplitsIncomeFromExpenses()
    {
        var trend = _sut.SummarizeByMonth(
            [Income(1, 400m, MidJune), Expense(2, -60m, MidJune)], MidJune, months: 1);

        var point = Assert.Single(trend);
        Assert.Equal((400m, 60m), (point.Income, point.Expenses));
    }

    [Fact]
    public void SummarizeByMonth_NonPositiveMonthsReturnsEmpty()
    {
        Assert.Empty(_sut.SummarizeByMonth([Expense(1, -10m, MidJune)], MidJune, months: 0));
        Assert.Empty(_sut.SummarizeByMonth([Expense(1, -10m, MidJune)], MidJune, months: -1));
    }
}
