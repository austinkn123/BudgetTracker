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

    // ---- AnalyzeMonth ----

    [Fact]
    public void SignedExpense_AggregatesAsPositiveMagnitude()
    {
        var result = _sut.AnalyzeMonth([Expense(1, -50m, MidJune)], Plan(ExpenseLine(1, 100m)), MidJune);

        Assert.Equal(50m, result.Pacing.ActualExpenses);
        Assert.Equal(50m, result.Expenses);
        var category = Assert.Single(result.ByCategory);
        Assert.Equal((1, 100m, 50m), (category.CategoryId, category.Planned, category.Actual));
    }

    [Fact]
    public void PlanMonthScope_ExcludesAdjacentMonths()
    {
        var result = _sut.AnalyzeMonth(
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
        var result = _sut.AnalyzeMonth(
            [Income(1, 1000m, MidJune), Expense(2, -250m, MidJune)],
            Plan(ExpenseLine(1, 100m)), MidJune);

        Assert.Equal(1000m, result.Income);
        Assert.Equal(250m, result.Expenses);
    }

    [Theory]
    [InlineData(2026, 6, 15, 15, 30)]   // mid-month, 30-day month
    [InlineData(2026, 8, 1, 1, 31)]     // first day of a 31-day month
    [InlineData(2026, 2, 28, 28, 28)]   // last day of February (2026 is not a leap year)
    public void DaysElapsed_TracksDayOfTheAnalysedMonth(
        int year, int month, int day, int expectedElapsed, int expectedDaysInMonth)
    {
        var result = _sut.AnalyzeMonth([], Plan(ExpenseLine(1, 100m)), new DateTime(year, month, day));

        Assert.Equal(expectedElapsed, result.Pacing.DaysElapsed);
        Assert.Equal(expectedDaysInMonth, result.Pacing.DaysInMonth);
    }

    [Theory]
    [InlineData(-45, PacingStatus.Ahead)]      // spentPct .45 vs daysPct .50 -> delta exactly -0.05
    [InlineData(-50, PacingStatus.OnTrack)]    // delta 0
    [InlineData(-55, PacingStatus.Behind)]     // delta exactly +0.05
    public void Status_ThresholdsAreInclusiveAtFivePercent(int signedSpend, PacingStatus expected)
    {
        var result = _sut.AnalyzeMonth([Expense(1, signedSpend, MidJune)], Plan(ExpenseLine(1, 100m)), MidJune);

        Assert.Equal(expected, result.Pacing.Status);
    }

    [Fact]
    public void ProjectionAndPerDiem_UseLinearRunRate()
    {
        var pacing = _sut.AnalyzeMonth([Expense(1, -45m, MidJune)], Plan(ExpenseLine(1, 100m)), MidJune).Pacing;

        Assert.Equal(90m, pacing.ProjectedEnd);        // 45 over 15 days, extrapolated to 30
        Assert.Equal(55m, pacing.Remaining);
        Assert.Equal(55m / 15, pacing.PerDiemToStay);
        Assert.Equal(0.45m, pacing.SpentPct);
    }

    [Fact]
    public void PlanRollsForward_AnalysesTheCurrentMonthNotThePlanMonth()
    {
        // The plan took effect in June; we are now in August. August actuals are what count,
        // and June spend must NOT leak in.
        var august = new DateTime(2026, 8, 20);

        var result = _sut.AnalyzeMonth(
            [Expense(1, -400m, MidJune), Expense(2, -60m, new DateTime(2026, 8, 5))],
            Plan(ExpenseLine(1, 100m)),
            august);

        Assert.Equal(new DateTime(2026, 8, 1), result.AnalyzedMonth);
        Assert.Equal(PlanMonth, result.Plan.PlanMonth);   // plan still reports when it took effect
        Assert.Equal(60m, result.Expenses);               // June is excluded
        Assert.Equal(20, result.Pacing.DaysElapsed);
        Assert.Equal(31, result.Pacing.DaysInMonth);
    }

    [Fact]
    public void OnTheLastDayOfTheMonth_PerDiemIsZero()
    {
        // No days left to spread the remainder over.
        var pacing = _sut.AnalyzeMonth(
            [Expense(1, -20m, MidJune)], Plan(ExpenseLine(1, 100m)), new DateTime(2026, 6, 30)).Pacing;

        Assert.Equal(30, pacing.DaysElapsed);
        Assert.Equal(0m, pacing.PerDiemToStay);
    }

    [Fact]
    public void NothingPlanned_SpentPctIsZeroRatherThanDivideByZero()
    {
        var pacing = _sut.AnalyzeMonth([Expense(1, -30m, MidJune)], Plan(), MidJune).Pacing;

        Assert.Equal(0m, pacing.SpentPct);
        Assert.Equal(30m, pacing.ActualExpenses);
    }

    [Fact]
    public void Buckets_UnplannedCategoryFallsBackToBuffer()
    {
        var result = _sut.AnalyzeMonth(
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
        var result = _sut.AnalyzeMonth([], Plan(), MidJune);

        Assert.Equal(["Core", "Buffer"], result.ByBucket.Select(b => b.Bucket));
    }

    [Fact]
    public void UncategorizedSpend_UsesNullCategoryIdAndLandsInBuffer()
    {
        var result = _sut.AnalyzeMonth(
            [Expense(1, -35m, MidJune, categoryId: null)], Plan(ExpenseLine(1, 100m)), MidJune);

        Assert.Contains(result.ByCategory, c => c.CategoryId is null && c.Actual == 35m);
        Assert.Equal(35m, result.ByBucket.Single(b => b.Bucket == "Buffer").Actual);
    }

    [Fact]
    public void ByCategory_UnionsPlannedAndActualAndExposesOverBy()
    {
        var result = _sut.AnalyzeMonth(
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

        var result = _sut.AnalyzeMonth(transactions, Plan(), MidJune);

        Assert.Equal(12, result.ByCategory.Count);
    }

    // ---- SummarizeSpend ----

    private static CategoryTypeTotal Total(int? categoryId, string type, decimal signedSum) => new(categoryId, type, signedSum);

    [Fact]
    public void SummarizeSpend_TotalsExpenseMagnitudePerCategoryAndIgnoresIncome()
    {
        var spend = _sut.SummarizeSpend(
        [
            Total(1, "Expense", -25m),
            Total(2, "Expense", -40m),
            Total(1, "Income", 500m)
        ]);

        Assert.Equal(2, spend.Count);
        Assert.Equal(25m, spend.Single(s => s.CategoryId == 1).Amount);
        Assert.Equal(40m, spend.Single(s => s.CategoryId == 2).Amount);
    }

    [Fact]
    public void SummarizeSpend_EmptyInputReturnsEmpty() => Assert.Empty(_sut.SummarizeSpend([]));

    [Fact]
    public void Should_IgnoreTransfers_When_SummarizingSpend()
    {
        var spend = _sut.SummarizeSpend([Total(1, "Transfer", -300m), Total(1, "Expense", -20m)]);

        Assert.Equal(new CategorySpend(1, 20m), Assert.Single(spend));
    }

    [Fact]
    public void Should_KeepUncategorizedSpendAsANullCategory_When_SummarizingSpend()
    {
        var spend = _sut.SummarizeSpend([Total(null, "Expense", -12m), Total(3, "Expense", -5m)]);

        Assert.Equal(12m, spend.Single(s => s.CategoryId is null).Amount);
        Assert.Equal(5m, spend.Single(s => s.CategoryId == 3).Amount);
    }

    [Fact]
    public void Should_MergeRepeatedCategoryRows_When_TheSameCategoryArrivesMoreThanOnce()
    {
        // The contract does not promise one row per (category, type); the engine must still produce one per category.
        var spend = _sut.SummarizeSpend([Total(1, "Expense", -80m), Total(1, "Expense", 30m)]);

        Assert.Equal(new CategorySpend(1, 50m), Assert.Single(spend));
    }

    [Fact]
    public void Should_ReportNegativeSpend_When_RefundsExceedOutflows()
    {
        var spend = _sut.SummarizeSpend([Total(1, "Expense", 15m)]);

        Assert.Equal(new CategorySpend(1, -15m), Assert.Single(spend));
    }

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

    // ---- Refunds net against spending (positive Expense rows) ----

    private static Transaction Typed(int id, string type, decimal amount, DateTime occurredAt, int? categoryId = 1) => new()
    {
        Id = id,
        AccountId = 1,
        CategoryId = categoryId,
        TransactionType = type,
        Amount = amount,
        OccurredAt = occurredAt
    };

    [Fact]
    public void AnalyzeMonth_Refund_ReducesActualSpend()
    {
        var result = _sut.AnalyzeMonth(
            [Expense(1, -80m, MidJune), Expense(2, 30m, MidJune)],
            Plan(ExpenseLine(1, 100m)), MidJune);

        Assert.Equal(50m, result.Expenses);
        Assert.Equal(50m, result.ByCategory.Single(c => c.CategoryId == 1).Actual);
        Assert.Equal(50m, result.ByBucket.Single(b => b.Bucket == "Core").Actual);
    }

    [Fact]
    public void SummarizeSpend_Refund_NetsWithinItsCategory()
    {
        // A refund is a positive Expense; in SQL it is already netted into the (category, Expense) sum.
        var spend = _sut.SummarizeSpend([Total(1, "Expense", -80m + 30m)]);

        Assert.Equal(50m, Assert.Single(spend).Amount);
    }

    [Fact]
    public void SummarizeByMonth_Refund_NetsAndTransfersAreNotSpending()
    {
        var trend = _sut.SummarizeByMonth(
            [
                Expense(1, -80m, MidJune),
                Expense(2, 30m, MidJune),
                Typed(3, "Transfer", -500m, MidJune),
                Typed(4, "Adjustment", -7m, MidJune)
            ],
            MidJune, months: 1);

        Assert.Equal(50m, Assert.Single(trend).Expenses);
    }

    // ---- Trend window helpers ----

    [Fact]
    public void SummarizeByMonth_SeedCategories_GetZeroedMonthsAlongsideThePresentOnes()
    {
        var trend = _sut.SummarizeByMonth([Expense(1, -10m, MidJune)], MidJune, months: 2, seedCategoryIds: [42, null]);

        Assert.Equal(6, trend.Count);
        Assert.All(trend.Where(p => p.CategoryId != 1), p => Assert.Equal((0m, 0m), (p.Income, p.Expenses)));
        Assert.Equal(new int?[] { null, 1, 42 }, trend.Select(p => p.CategoryId).Distinct().Order());
    }

    [Fact]
    public void SummarizeByMonth_SeedCategoryAlsoPresent_IsNotDuplicated()
    {
        var categoryId = Expense(1, -10m, MidJune).CategoryId;

        var trend = _sut.SummarizeByMonth([Expense(1, -10m, MidJune)], MidJune, months: 1, seedCategoryIds: [categoryId]);

        Assert.Equal(10m, Assert.Single(trend).Expenses);
    }

    [Theory]
    [InlineData(1, 2026, 6)]
    [InlineData(3, 2026, 4)]
    [InlineData(12, 2025, 7)]
    [InlineData(24, 2024, 7)]
    public void TrendStart_IsTheFirstDayOfTheOldestMonthInTheWindow(int months, int year, int month)
    {
        Assert.Equal(new DateTime(year, month, 1), _sut.TrendStart(MidJune, months));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(24)]
    public void ValidateTrendMonths_AcceptsOneToTwentyFour(int months) =>
        Assert.Null(_sut.ValidateTrendMonths(months));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(25)]
    [InlineData(int.MaxValue)]
    public void ValidateTrendMonths_RejectsOutOfRange(int months) =>
        Assert.Equal("trendMonths must be between 1 and 24", _sut.ValidateTrendMonths(months));
}
