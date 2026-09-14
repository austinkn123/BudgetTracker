using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.Analysis;

namespace BudgetTracker.Domain.Interfaces.Engines;

public interface IBudgetAnalysisEngine
{
    /// <summary>
    /// Analyse actuals for the month containing <paramref name="now"/> against the supplied plan.
    ///
    /// A plan rolls forward: its <see cref="BudgetPlan.PlanMonth"/> is the month it TAKES EFFECT,
    /// not the only month it governs, so the analysed month and the plan month differ once the
    /// plan is older than the current month. Scoping to a single month is a business rule (a plan
    /// states monthly amounts), so it happens here rather than in the caller.
    /// </summary>
    PlanPerformance AnalyzeMonth(IReadOnlyList<Transaction> transactions, BudgetPlan plan, DateTime now);

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
