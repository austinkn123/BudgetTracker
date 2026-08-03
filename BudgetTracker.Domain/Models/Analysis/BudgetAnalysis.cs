namespace BudgetTracker.Domain.Models.Analysis;

/// <summary>
/// A complete numeric analysis of a user's budget. Deliberately carries no presentation
/// decisions — nothing here is ordered, truncated, labelled, or formatted. Callers select
/// what to display. A null <see cref="CategoryId"/> means "uncategorized".
/// </summary>
public record BudgetAnalysis(
    PlanPerformance? PlanMonth,
    IReadOnlyList<CategorySpend> WindowSpend,
    IReadOnlyList<CategoryMonthSpend> MonthlyTrend);

/// <summary>Performance of the active plan against actuals for the plan's own month.</summary>
public record PlanPerformance(
    AnalyzedPlan Plan,
    PeriodPacing Pacing,
    IReadOnlyList<CategoryPerformance> ByCategory,
    IReadOnlyList<BucketPerformance> ByBucket,
    decimal Income,
    decimal Expenses);

public record AnalyzedPlan(int Id, string Name, DateTime PlanMonth);

/// <summary>How spending is tracking against the plan relative to how much of the month has passed.</summary>
public record PeriodPacing(
    int DaysElapsed,
    int DaysInMonth,
    decimal DaysPct,
    decimal SpentPct,
    decimal ProjectedEnd,
    decimal PacingDelta,
    PacingStatus Status,
    decimal PlannedExpenses,
    decimal ActualExpenses,
    decimal Remaining,
    decimal PerDiemToStay);

public record CategoryPerformance(int? CategoryId, decimal Planned, decimal Actual)
{
    /// <summary>Positive when actual spend has exceeded the planned amount.</summary>
    public decimal OverBy => Actual - Planned;
}

public record BucketPerformance(string Bucket, decimal Planned, decimal Actual);

/// <summary>Total expense magnitude for one category over the requested window.</summary>
public record CategorySpend(int? CategoryId, decimal Amount);

/// <summary>One category's income/expense totals for a single calendar month.</summary>
public record CategoryMonthSpend(int? CategoryId, DateTime Month, decimal Income, decimal Expenses);

public enum PacingStatus
{
    Ahead,
    OnTrack,
    Behind
}
