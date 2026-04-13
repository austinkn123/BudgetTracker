using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Managers;

public interface IBudgetPlanManager
{
    Task<Result<BudgetPlan>> GetByIdAsync(int id, int userId);
    Task<Result<IEnumerable<BudgetPlan>>> GetByUserIdAsync(int userId);
    Task<Result<int>> CreateAsync(BudgetPlan budgetPlan, int userId);
    Task<Result<bool>> UpdateAsync(BudgetPlan budgetPlan, int userId);
    Task<Result<bool>> DeleteAsync(int id, int userId);
}