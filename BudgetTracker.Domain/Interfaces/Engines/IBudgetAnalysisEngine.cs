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
    /// Net expense per category from per-(category, type) signed totals: outflows minus refunds, with Income and Transfer
    /// totals ignored. The totals are pre-aggregated by the caller over whatever window it chose — the window is not a
    /// business concept — so a long window never has to be loaded row by row.
    /// </summary>
    /// <param name="totals">Signed sums per (category, type); a category may appear more than once.</param>
    /// <returns>One entry per category with Expense totals; positive is net spend, negative is net refund.</returns>
    IReadOnlyList<CategorySpend> SummarizeSpend(IReadOnlyList<CategoryTypeTotal> totals);

    /// <summary>
    /// Per-category income/expense totals for the last <paramref name="months"/> calendar months
    /// ending with the month containing <paramref name="now"/>. Every category present is seeded
    /// with zeroed months so trend series are gap-free.
    /// </summary>
    /// <param name="transactions">Rows covering at least the trend window; rows outside it only seed their category.</param>
    /// <param name="now">An instant in the newest month of the window.</param>
    /// <param name="months">Window length.</param>
    /// <param name="seedCategoryIds">
    /// Further categories (null = uncategorized) to seed with zeroed months even when none of
    /// <paramref name="transactions"/> carries them, so a caller can fetch just the window yet keep every category
    /// the user has ever used in the series.
    /// </param>
    IReadOnlyList<CategoryMonthSpend> SummarizeByMonth(
        IReadOnlyList<Transaction> transactions, DateTime now, int months, IEnumerable<int?>? seedCategoryIds = null);

    /// <summary>The first day of the trend window <see cref="SummarizeByMonth"/> covers.</summary>
    /// <param name="now">An instant in the newest month of the window.</param>
    /// <param name="months">Window length; at least 1.</param>
    /// <returns>The first day of the oldest month in the window.</returns>
    DateTime TrendStart(DateTime now, int months);

    /// <summary>Validate a requested trend length: 1 to 24 months.</summary>
    /// <param name="months">The requested trend length.</param>
    /// <returns>Null when valid, otherwise the error message.</returns>
    string? ValidateTrendMonths(int months);
}
