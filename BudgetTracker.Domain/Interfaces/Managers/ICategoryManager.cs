using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Managers;

public interface ICategoryManager
{
    Task<Result<Category>> GetByIdAsync(int id, int userId);
    Task<Result<IEnumerable<Category>>> GetByUserIdAsync(int userId);
    Task<Result<int>> CreateAsync(Category category);
    Task<Result<bool>> UpdateAsync(Category category);
    Task<Result<bool>> DeleteAsync(int id, int userId);

    /// <summary>Per-category usage: all-time row count, and inflow/outflow since <paramref name="since"/>.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="since">The instant the inflow/outflow sums start at (inclusive).</param>
    /// <returns>One entry per category holding at least one of the user's rows.</returns>
    Task<Result<IReadOnlyList<CategoryUsage>>> GetUsageAsync(int userId, DateTime since);
}
