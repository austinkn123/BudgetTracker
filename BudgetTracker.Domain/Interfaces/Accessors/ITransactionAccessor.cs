using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

public interface ITransactionAccessor
{
    /// <summary>The user's whole ledger, newest first. Budget analysis needs all of it.</summary>
    Task<IEnumerable<Transaction>> GetByUserIdAsync(int userId);

    /// <summary>The user's ledger narrowed server-side, newest first.</summary>
    Task<IEnumerable<Transaction>> GetFilteredAsync(int userId, TransactionFilter filter);

    /// <summary>The rows with these ids that <paramref name="userId"/> owns; ids owned by anyone else are skipped.</summary>
    Task<IReadOnlyList<Transaction>> GetByIdsAsync(IEnumerable<int> ids, int userId);

    /// <summary>
    /// The category on the user's newest categorized row for each payee, keyed case-insensitively. Sync uses it to
    /// file a new row the way the user last filed that payee.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetLatestCategoryByPayeeAsync(int userId);

    /// <summary>Already-imported rows carrying any of these Plaid transaction ids.</summary>
    Task<IReadOnlyList<Transaction>> GetByPlaidTransactionIdsAsync(IEnumerable<string> plaidTransactionIds);

    /// <summary>
    /// Set (or clear, when <paramref name="categoryId"/> is null) the category on many rows at once, writing each row's
    /// re-derived type from <paramref name="transactionTypes"/> (keyed by row id) in the same save.
    /// Only rows owned by <paramref name="userId"/> are touched. Returns how many changed.
    /// </summary>
    Task<int> SetCategoryAsync(IReadOnlyDictionary<int, string> transactionTypes, int? categoryId, int userId);

    /// <summary>
    /// The user's posted rows carrying one of <paramref name="plaidCategoryPrimaries"/>, Transfer legs included:
    /// the pool own-account transfer pairing re-pairs after every sync.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetTransferCandidatesAsync(int userId, IReadOnlyCollection<string> plaidCategoryPrimaries);

    /// <summary>Set each row's type and transfer account as given, in one save.</summary>
    Task ApplyTransferUpdatesAsync(IReadOnlyList<TransferUpdate> updates);

    /// <summary>Set (or clear) the note on one row the user owns.</summary>
    Task<bool> SetNotesAsync(int id, string? notes, int userId);

    /// <summary>
    /// Upsert a batch of Plaid-imported transactions, keyed by <see cref="Transaction.PlaidTransactionId"/>.
    /// Inserts new rows; updates merchant/amount/date/pending/type for existing ones, except that a row already paired as
    /// a Transfer keeps its type. Returns counts (inserted, updated).
    /// </summary>
    Task<(int Inserted, int Updated)> UpsertImportedAsync(IEnumerable<Transaction> transactions);

    /// <summary>Delete imported transactions by Plaid transaction id (used when Plaid reports removals).</summary>
    Task<int> DeleteByPlaidTransactionIdsAsync(IEnumerable<string> plaidTransactionIds);
}
