using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

public interface ITransactionAccessor
{
    /// <summary>The user's ledger narrowed server-side, newest first.</summary>
    Task<IEnumerable<Transaction>> GetFilteredAsync(int userId, TransactionFilter filter);

    /// <summary>
    /// Up to <paramref name="take"/> of the user's rows matching <paramref name="filter"/>, in OccurredAt DESC, Id DESC
    /// order, starting strictly after <paramref name="after"/> (keyset pagination; null starts at the newest row).
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <param name="filter">The same optional filters as <see cref="GetFilteredAsync"/>.</param>
    /// <param name="after">The last row of the previous page, or null for the first page.</param>
    /// <param name="take">The maximum number of rows to return.</param>
    /// <returns>The rows, newest first.</returns>
    Task<IReadOnlyList<Transaction>> GetPageAsync(int userId, TransactionFilter filter, TransactionCursor? after, int take);

    /// <summary>
    /// Ledger-wide counters, aggregated in the database: uncategorized non-Transfer rows, and pending amount per Plaid
    /// account.
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <returns>The summary; pending totals are ordered by Plaid account id.</returns>
    Task<TransactionSummary> GetSummaryAsync(int userId);

    /// <summary>
    /// Per-category usage for every category holding at least one of the user's rows, aggregated in the database.
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <param name="since">The instant the inflow/outflow sums start at (inclusive).</param>
    /// <returns>One entry per used category, ordered by category id.</returns>
    Task<IReadOnlyList<CategoryUsage>> GetCategoryUsageAsync(int userId, DateTime since);

    /// <summary>
    /// Every distinct <see cref="Transaction.CategoryId"/> on the user's rows, null (uncategorized) included.
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <returns>The distinct category ids.</returns>
    Task<IReadOnlyList<int?>> GetCategoryIdsInUseAsync(int userId);

    /// <summary>
    /// The sum of signed amounts per Plaid account over the user's posted rows dated after <paramref name="after"/>'s day.
    /// Cash flow undoes these to walk balances back to its window without loading the rows.
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <param name="after">Rows dated on later days than this one count.</param>
    /// <returns>Sum by Plaid account id; accounts with no such rows are absent.</returns>
    Task<IReadOnlyDictionary<string, decimal>> GetPostedSumByPlaidAccountAfterAsync(int userId, DateTime after);

    /// <summary>
    /// The user's newest Income rows in <paramref name="categoryIds"/> dated on or before <paramref name="onOrBefore"/>'s
    /// day, newest first (OccurredAt DESC, Id DESC).
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <param name="categoryIds">Only rows in these categories.</param>
    /// <param name="onOrBefore">The last day considered (inclusive).</param>
    /// <param name="minAmount">When set, only rows with at least this amount.</param>
    /// <param name="take">The maximum number of rows to return.</param>
    /// <returns>The rows, newest first.</returns>
    Task<IReadOnlyList<Transaction>> GetNewestIncomeAsync(
        int userId, IReadOnlyCollection<int> categoryIds, DateTime onOrBefore, decimal? minAmount, int take);

    /// <summary>The rows with these ids that <paramref name="userId"/> owns; ids owned by anyone else are skipped.</summary>
    Task<IReadOnlyList<Transaction>> GetByIdsAsync(IEnumerable<int> ids, int userId);

    /// <summary>
    /// For each of <paramref name="payees"/> (matched case-insensitively), the category on the user's newest categorized
    /// row with that payee, picked in the database. Sync uses it to file a new row the way the user last filed that payee.
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <param name="payees">The payees in the current sync batch.</param>
    /// <returns>Category id by payee, keyed case-insensitively; payees never categorized are absent.</returns>
    Task<IReadOnlyDictionary<string, int>> GetLatestCategoryByPayeeAsync(int userId, IReadOnlyCollection<string> payees);

    /// <summary>Already-imported rows carrying any of these Plaid transaction ids.</summary>
    Task<IReadOnlyList<Transaction>> GetByPlaidTransactionIdsAsync(IEnumerable<string> plaidTransactionIds);

    /// <summary>
    /// Set (or clear, when <paramref name="categoryId"/> is null) the category on many rows at once, writing each row's
    /// re-derived type from <paramref name="transactionTypes"/> (keyed by row id) in the same save.
    /// Only rows owned by <paramref name="userId"/> are touched. Returns how many changed.
    /// </summary>
    Task<int> SetCategoryAsync(IReadOnlyDictionary<int, string> transactionTypes, int? categoryId, int userId);

    /// <summary>
    /// Every posted row of the user's carrying one of <paramref name="plaidCategoryPrimaries"/>, Transfer legs included:
    /// the pool own-account transfer pairing re-pairs from scratch after a sync. Read-only and projected to the columns
    /// pairing uses (id, accounts, amount, date, Plaid category, pending, type, transfer account, category).
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
