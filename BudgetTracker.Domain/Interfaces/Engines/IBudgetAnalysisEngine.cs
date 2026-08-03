using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.Analysis;

namespace BudgetTracker.Domain.Interfaces.Engines;

public interface IBudgetAnalysisEngine
{
    /// <summary>
    /// Analyse the plan against actuals for the plan's own month. Scoping to that month is a
    /// business rule (the plan defines its period), so it happens here rather than in the caller.
    /// <paramref name="now"/> drives pacing and is passed in to keep the engine deterministic.
    /// </summary>
    PlanPerformance AnalyzePlanMonth(IReadOnlyList<Transaction> transactions, BudgetPlan plan, DateTime now);

    /// <summary>
    /// Total expense magnitude per category across whatever transactions are supplied.
    /// Callers do their own date filtering — the window is not a business concept.
    /// </summary>
    IReadOnlyList<CategorySpend> SummarizeSpend(IReadOnlyList<Transaction> transactions);

    /// <summary>
    /// Per-category income/expense totals for the last <paramref name="months"/> calendar months
    /// ending with the month containing <paramref name="now"/>. Every category present is seeded
    /// with zeroed months so trend series are gap-free.
    /// </summary>
    IReadOnlyList<CategoryMonthSpend> SummarizeByMonth(IReadOnlyList<Transaction> transactions, DateTime now, int months);
}
