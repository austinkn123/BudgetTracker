using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

public interface ICategoryAccessor
{
    Task<Category> GetByIdAsync(int id);
    Task<IEnumerable<Category>> GetByUserIdAsync(int userId);
    Task<int> CreateAsync(Category category);
    Task<bool> UpdateAsync(Category category);
    Task<bool> DeleteAsync(int id);
}
