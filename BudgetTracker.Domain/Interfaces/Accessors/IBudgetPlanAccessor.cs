using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

public interface IBudgetPlanAccessor
{
    Task<BudgetPlan?> GetByIdAsync(int id, int userId);
    Task<IEnumerable<BudgetPlan>> GetByUserIdAsync(int userId);
    Task<bool> CategoriesBelongToUserAsync(IEnumerable<int> categoryIds, int userId);

    /// <summary>Inserts <paramref name="budgetPlan"/> with its entries and returns its new id.</summary>
    /// <param name="budgetPlan">The plan to insert.</param>
    /// <returns>The new plan id.</returns>
    /// <exception cref="UniqueNameViolationException">The user already has a plan with this name for the month, ignoring case.</exception>
    Task<int> CreateAsync(BudgetPlan budgetPlan);

    /// <summary>Names of the user's plans for <paramref name="planMonth"/> other than <paramref name="excludeBudgetPlanId"/>.</summary>
    /// <param name="userId">The owning user.</param>
    /// <param name="planMonth">The plan month (first day of the month).</param>
    /// <param name="excludeBudgetPlanId">The plan being updated, or 0 when creating.</param>
    /// <returns>The other plan names for that month.</returns>
    Task<IReadOnlyList<string>> GetOtherNamesForMonthAsync(int userId, DateTime planMonth, int excludeBudgetPlanId);

    /// <summary>Saves <paramref name="budgetPlan"/> and reconciles its entries.</summary>
    /// <param name="budgetPlan">The plan to save.</param>
    /// <param name="userId">The owning user.</param>
    /// <returns>False when the plan or one of its entries does not exist for the user.</returns>
    /// <exception cref="UniqueNameViolationException">The user already has a plan with this name for the month, ignoring case.</exception>
    Task<bool> UpdateAsync(BudgetPlan budgetPlan, int userId);
    Task<bool> DeleteAsync(int id, int userId);
}