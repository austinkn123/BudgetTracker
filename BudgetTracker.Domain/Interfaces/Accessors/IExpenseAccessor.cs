using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

public interface IExpenseAccessor
{
    Task<Expense> GetByIdAsync(int id);
    Task<IEnumerable<Expense>> GetByUserIdAsync(int userId);
    Task<int> CreateAsync(Expense expense);
    Task<bool> UpdateAsync(Expense expense);
    Task<bool> DeleteAsync(int id);
}
