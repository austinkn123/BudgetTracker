using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Server.Managers;

/// <summary>
/// Read and review orchestration for the ledger. Plaid sync creates every transaction (via
/// <see cref="ITransactionAccessor.UpsertImportedAsync"/>), so this manager owns no create or
/// delete path — only the two annotations a person makes: category and notes.
/// </summary>
public class TransactionManager(ITransactionAccessor accessor, ICategoryAccessor categoryAccessor) : ITransactionManager
{
    public async Task<Result<IEnumerable<Transaction>>> GetByUserIdAsync(int userId)
    {
        var transactions = await accessor.GetByUserIdAsync(userId);
        return Result<IEnumerable<Transaction>>.Success(transactions);
    }

    public async Task<Result<IEnumerable<Transaction>>> GetFilteredAsync(int userId, TransactionFilter filter)
    {
        var transactions = await accessor.GetFilteredAsync(userId, filter);
        return Result<IEnumerable<Transaction>>.Success(transactions);
    }

    public async Task<Result<int>> SetCategoryAsync(IEnumerable<int> ids, int? categoryId, int userId)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return Result<int>.Failure("No transactions selected");

        // Verify the target category is the user's own before writing it across a batch; the accessor
        // scopes the rows by owner, but the category id arrives straight from the request body.
        if (categoryId is int id)
        {
            var category = await categoryAccessor.GetByIdForUserAsync(id, userId);
            if (category is null)
                return Result<int>.Failure("Category not found");
        }

        var changed = await accessor.SetCategoryAsync(idList, categoryId, userId);
        return Result<int>.Success(changed);
    }

    public async Task<Result<bool>> SetNotesAsync(int id, string? notes, int userId)
    {
        // Matches the column width; the ledger is not a place for essays.
        if (notes is { Length: > 1000 })
            return Result<bool>.Failure("Notes must be 1000 characters or fewer");

        var normalised = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        var updated = await accessor.SetNotesAsync(id, normalised, userId);
        return updated
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Transaction not found");
    }
}
