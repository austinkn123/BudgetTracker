using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Server.Managers;

/// <summary>
/// Read and review orchestration for the ledger. Plaid sync creates every transaction (via
/// <see cref="ITransactionAccessor.UpsertImportedAsync"/>), so this manager owns no create or
/// delete path — only the two annotations a person makes: category and notes.
/// </summary>
public class TransactionManager(
    ITransactionAccessor accessor,
    ICategoryAccessor categoryAccessor,
    IPlaidEngine plaidEngine,
    ITransactionEngine transactionEngine) : ITransactionManager
{
    private const string FilterRequired =
        "At least one filter is required; read the whole ledger a page at a time from GET /api/transactions/page.";

    /// <inheritdoc />
    public async Task<Result<IEnumerable<Transaction>>> GetFilteredAsync(int userId, TransactionFilter filter)
    {
        if (!transactionEngine.HasAnyFilter(filter))
            return Result<IEnumerable<Transaction>>.Failure(FilterRequired);

        var transactions = await accessor.GetFilteredAsync(userId, filter);
        return Result<IEnumerable<Transaction>>.Success(transactions);
    }

    /// <inheritdoc />
    public async Task<Result<TransactionPage>> GetPageAsync(int userId, TransactionFilter filter, string? cursor, int? limit)
    {
        var after = transactionEngine.ParseCursor(cursor);
        if (!after.IsSuccess)
            return Result<TransactionPage>.Failure(after.Error!);

        // One row past the page tells the engine whether another page exists, without a COUNT.
        var pageSize = transactionEngine.ResolvePageSize(limit);
        var fetched = await accessor.GetPageAsync(userId, filter, after.Value, pageSize + 1);
        return Result<TransactionPage>.Success(transactionEngine.BuildPage(fetched, pageSize));
    }

    /// <inheritdoc />
    public async Task<Result<TransactionSummary>> GetSummaryAsync(int userId)
    {
        var summary = await accessor.GetSummaryAsync(userId);
        return Result<TransactionSummary>.Success(summary);
    }

    public async Task<Result<int>> SetCategoryAsync(IEnumerable<int> ids, int? categoryId, int userId)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return Result<int>.Failure("No transactions selected");

        // Verify the target category is the user's own before writing it across a batch; the accessor
        // scopes the rows by owner, but the category id arrives straight from the request body.
        Category? category = null;
        if (categoryId is int id)
        {
            category = await categoryAccessor.GetByIdForUserAsync(id, userId);
            if (category is null)
                return Result<int>.Failure("Category not found");
        }

        // An Income- or Expense-typed category overrides Plaid's classification, so each row's type is re-derived
        // from the category it is moving to (Transfer rows keep theirs).
        var rows = await accessor.GetByIdsAsync(idList, userId);
        var types = rows.ToDictionary(
            r => r.Id,
            r => plaidEngine.ClassifyTransactionType(r.PlaidCategoryPrimary, category?.CategoryType, r.TransactionType));

        var changed = await accessor.SetCategoryAsync(types, categoryId, userId);
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
