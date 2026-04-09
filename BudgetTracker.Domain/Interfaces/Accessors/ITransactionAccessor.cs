using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

public interface ITransactionAccessor
{
    Task<Transaction?> GetByIdAsync(int id, int userId);
    Task<IEnumerable<Transaction>> GetByUserIdAsync(int userId);
    Task<bool> AccountBelongsToUserAsync(int accountId, int userId);
    Task<int> CreateAsync(Transaction transaction);
    Task<bool> UpdateAsync(Transaction transaction, int userId);
    Task<bool> DeleteAsync(int id, int userId);
}
