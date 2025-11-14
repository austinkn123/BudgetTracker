using BudgetTracker.Core.Models;

namespace BudgetTracker.Application.Interfaces
{
    public interface IExpenseRepository
    {
        Task<Expense> GetByIdAsync(int id);
        Task<IEnumerable<Expense>> GetByUserIdAsync(int userId);
        Task<int> CreateAsync(Expense expense);
        Task<bool> UpdateAsync(Expense expense);
        Task<bool> DeleteAsync(int id);
    }
}
