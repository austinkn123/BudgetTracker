using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

public interface IBudgetPlanAccessor
{
    Task<BudgetPlan?> GetByIdAsync(int id, int userId);
    Task<IEnumerable<BudgetPlan>> GetByUserIdAsync(int userId);
    Task<bool> CategoriesBelongToUserAsync(IEnumerable<int> categoryIds, int userId);
    Task<int> CreateAsync(BudgetPlan budgetPlan);
    Task<bool> UpdateAsync(BudgetPlan budgetPlan, int userId);
    Task<bool> DeleteAsync(int id, int userId);
}