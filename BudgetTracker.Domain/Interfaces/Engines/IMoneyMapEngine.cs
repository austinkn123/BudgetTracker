using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.MoneyMap;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>Pure money-map arithmetic: plan vs monthly averages, projected lump-sum bills and extra paychecks. No I/O.</summary>
public interface IMoneyMapEngine
{
    /// <summary>
    /// Build the money map over the <paramref name="months"/> calendar months ending with today's. Only Income and
    /// Expense rows count; transfers and adjustments are money moving, not spending.
    /// </summary>
    /// <param name="plans">The user's plans with entries; the newest active one (by plan month) is used.</param>
    /// <param name="transactions">The user's ledger; at least the last 12 months for the projections.</param>
    /// <param name="categories">The user's categories, for line names.</param>
    /// <param name="today">The current date; the window ends on it and projections start after it.</param>
    /// <param name="months">Window length, 1 to 24.</param>
    /// <returns>The map, or a failure when <paramref name="months"/> is out of range.</returns>
    Result<MoneyMap> Build(
        IEnumerable<BudgetPlan> plans,
        IReadOnlyList<Transaction> transactions,
        IEnumerable<Category> categories,
        DateTime today,
        int months);
}
