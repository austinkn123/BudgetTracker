using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Managers;

public interface ITransactionManager
{
    Task<Result<Transaction>> GetByIdAsync(int id, int userId);
    Task<Result<IEnumerable<Transaction>>> GetByUserIdAsync(int userId);
    Task<Result<int>> CreateAsync(Transaction transaction, int userId);
    Task<Result<bool>> UpdateAsync(Transaction transaction, int userId);
    Task<Result<bool>> DeleteAsync(int id, int userId);
}
