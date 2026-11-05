using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Managers;

public interface ITransactionManager
{
    /// <summary>
    /// The ledger narrowed by <paramref name="filter"/>, for the month review page. Fails when no filter is set: the whole
    /// ledger is only served a page at a time by <see cref="GetPageAsync"/>.
    /// </summary>
    Task<Result<IEnumerable<Transaction>>> GetFilteredAsync(int userId, TransactionFilter filter);

    /// <summary>One keyset page of the ledger, newest first.</summary>
    /// <param name="userId">The owner.</param>
    /// <param name="filter">Optional filters, as for <see cref="GetFilteredAsync"/>.</param>
    /// <param name="cursor">The previous page's opaque next cursor, or null for the first page.</param>
    /// <param name="limit">Requested page size; defaults to 50 and is clamped to 1–200.</param>
    /// <returns>The page, or a failure when <paramref name="cursor"/> is malformed.</returns>
    Task<Result<TransactionPage>> GetPageAsync(int userId, TransactionFilter filter, string? cursor, int? limit);

    /// <summary>Ledger-wide counters: uncategorized rows and pending totals per Plaid account.</summary>
    /// <param name="userId">The owner.</param>
    /// <returns>The summary.</returns>
    Task<Result<TransactionSummary>> GetSummaryAsync(int userId);

    /// <summary>Set or clear the category on many transactions at once. Returns how many changed.</summary>
    Task<Result<int>> SetCategoryAsync(IEnumerable<int> ids, int? categoryId, int userId);

    /// <summary>Set or clear the note on one transaction.</summary>
    Task<Result<bool>> SetNotesAsync(int id, string? notes, int userId);
}
