using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Managers;

public interface IExpenseManager
{
    Task<Result<Expense>> GetByIdAsync(int id);
    Task<Result<IEnumerable<Expense>>> GetByUserIdAsync(int userId);
    Task<Result<int>> CreateAsync(Expense expense);
    Task<Result<bool>> UpdateAsync(Expense expense);
    Task<Result<bool>> DeleteAsync(int id);
}
