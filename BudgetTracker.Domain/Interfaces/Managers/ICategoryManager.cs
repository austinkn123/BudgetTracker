using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Managers;

public interface ICategoryManager
{
    Task<Result<Category>> GetByIdAsync(int id);
    Task<Result<IEnumerable<Category>>> GetByUserIdAsync(int userId);
    Task<Result<int>> CreateAsync(Category category);
    Task<Result<bool>> UpdateAsync(Category category);
    Task<Result<bool>> DeleteAsync(int id);
}
