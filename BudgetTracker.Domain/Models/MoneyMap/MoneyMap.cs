namespace BudgetTracker.Domain.Models.MoneyMap;

/// <summary>
/// Where a normal month's pay goes: the newest active plan beside what was actually spent, per category, plus the
/// lump-sum bills and extra paychecks ahead.
/// </summary>
/// <param name="PlanName">The newest active plan, or null when the user has none.</param>
/// <param name="From">First day of the window the actuals average over.</param>
/// <param name="To">Last day of the window (today).</param>
/// <param name="Months">Calendar months in the window; every actual is a monthly average over this many.</param>
/// <param name="PlannedIncomeMonthly">A normal month's pay, per the plan.</param>
/// <param name="ActualIncomeMonthly">Income that actually landed, averaged over the window.</param>
public record MoneyMap(
    string? PlanName,
    DateTime From,
    DateTime To,
    int Months,
    decimal PlannedIncomeMonthly,
    decimal ActualIncomeMonthly,
    IReadOnlyList<MoneyMapLine> Lines,
    IReadOnlyList<UpcomingItem> Upcoming);

/// <summary>
/// One plan line or spending category. Unplanned spending has a null <paramref name="Bucket"/>; uncategorized spending
/// also has a null <paramref name="CategoryId"/>.
/// </summary>
/// <param name="Bucket">Core (must-pay) or Buffer (flexible), from the plan.</param>
/// <param name="Cadence">Monthly or Annual, from the plan; Annual lines are lump-sum bills set aside monthly.</param>
/// <param name="PlanMonthly">Monthly equivalent planned; 0 when unplanned.</param>
/// <param name="ActualMonthly">Net spend over the window divided by its months; refunds net against it.</param>
/// <param name="TopPayees">The biggest payees by spend in the window, largest first, at most three.</param>
public record MoneyMapLine(
    int? CategoryId,
    string Name,
    string? Bucket,
    string? Cadence,
    decimal PlanMonthly,
    decimal ActualMonthly,
    IReadOnlyList<string> TopPayees,
    string? Notes);

public enum UpcomingKind
{
    /// <summary>A lump-sum bill projected from when it was last paid.</summary>
    Bill,

    /// <summary>A third paycheck in one calendar month (biweekly pay).</summary>
    ExtraPaycheck
}

/// <summary>Something due or arriving in the next 12 months.</summary>
public record UpcomingItem(DateTime Date, string Label, decimal Amount, UpcomingKind Kind);
