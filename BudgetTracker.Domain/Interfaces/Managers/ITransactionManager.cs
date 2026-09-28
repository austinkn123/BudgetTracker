using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Managers;

public interface ITransactionManager
{
    Task<Result<IEnumerable<Transaction>>> GetByUserIdAsync(int userId);

    /// <summary>The ledger narrowed by <paramref name="filter"/>, for the month review page.</summary>
    Task<Result<IEnumerable<Transaction>>> GetFilteredAsync(int userId, TransactionFilter filter);

    /// <summary>Set or clear the category on many transactions at once. Returns how many changed.</summary>
    Task<Result<int>> SetCategoryAsync(IEnumerable<int> ids, int? categoryId, int userId);

    /// <summary>Set or clear the note on one transaction.</summary>
    Task<Result<bool>> SetNotesAsync(int id, string? notes, int userId);
}
