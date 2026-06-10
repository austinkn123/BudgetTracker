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

    /// <summary>
    /// Upsert a batch of Plaid-imported transactions, keyed by <see cref="Transaction.PlaidTransactionId"/>.
    /// Inserts new rows; updates merchant/amount/date/pending for existing ones. Returns counts (inserted, updated).
    /// </summary>
    Task<(int Inserted, int Updated)> UpsertImportedAsync(IEnumerable<Transaction> transactions);

    /// <summary>Delete imported transactions by Plaid transaction id (used when Plaid reports removals).</summary>
    Task<int> DeleteByPlaidTransactionIdsAsync(IEnumerable<string> plaidTransactionIds);
}
