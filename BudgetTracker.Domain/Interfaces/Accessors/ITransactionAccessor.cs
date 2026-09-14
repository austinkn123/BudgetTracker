using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

public interface ITransactionAccessor
{
    /// <summary>The user's whole ledger, newest first. Budget analysis needs all of it.</summary>
    Task<IEnumerable<Transaction>> GetByUserIdAsync(int userId);

    /// <summary>The user's ledger narrowed server-side, newest first.</summary>
    Task<IEnumerable<Transaction>> GetFilteredAsync(int userId, TransactionFilter filter);

    /// <summary>
    /// Set (or clear, when <paramref name="categoryId"/> is null) the category on many rows at once.
    /// Only rows owned by <paramref name="userId"/> are touched. Returns how many changed.
    /// </summary>
    Task<int> SetCategoryAsync(IEnumerable<int> ids, int? categoryId, int userId);

    /// <summary>Set (or clear) the note on one row the user owns.</summary>
    Task<bool> SetNotesAsync(int id, string? notes, int userId);

    /// <summary>
    /// Upsert a batch of Plaid-imported transactions, keyed by <see cref="Transaction.PlaidTransactionId"/>.
    /// Inserts new rows; updates merchant/amount/date/pending for existing ones. Returns counts (inserted, updated).
    /// </summary>
    Task<(int Inserted, int Updated)> UpsertImportedAsync(IEnumerable<Transaction> transactions);

    /// <summary>Delete imported transactions by Plaid transaction id (used when Plaid reports removals).</summary>
    Task<int> DeleteByPlaidTransactionIdsAsync(IEnumerable<string> plaidTransactionIds);
}
