using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

public class TransactionAccessor(BudgetTrackerDbContext context) : ITransactionAccessor
{
    private const string TransferType = "Transfer";

    public async Task<IEnumerable<Transaction>> GetByUserIdAsync(int userId)
    {
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId)
            .OrderByDescending(t => t.OccurredAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetFilteredAsync(int userId, TransactionFilter filter)
    {
        var query = context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId);

        {
            // Compare on calendar days, matching BudgetAnalysisManager — otherwise the ledger and
            // the analysis disagree about which rows are in the month. The end bound is an
            // exclusive next-midnight rather than a CAST to date: it includes a row with any time
            // component, and it stays sargable so IX_Transactions_AccountId_OccurredAt is still used.
            if (filter.From is DateTime from)
            {
                var fromDate = from.Date;
                query = query.Where(t => t.OccurredAt >= fromDate);
            }

            if (filter.To is DateTime to)
            {
                var dayAfterTo = to.Date.AddDays(1);
                query = query.Where(t => t.OccurredAt < dayAfterTo);
            }

            if (filter.Uncategorized is bool uncategorized)
                query = uncategorized
                    ? query.Where(t => t.CategoryId == null)
                    : query.Where(t => t.CategoryId != null);

            if (filter.IsImported is bool imported)
                query = query.Where(t => t.IsImported == imported);

            if (filter.IsPending is bool pending)
                query = query.Where(t => t.IsPending == pending);

            if (!string.IsNullOrWhiteSpace(filter.PlaidAccountId))
                query = query.Where(t => t.PlaidAccountId == filter.PlaidAccountId);

            if (filter.CategoryId is int categoryId)
                query = query.Where(t => t.CategoryId == categoryId);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var pattern = LikePattern.Contains(filter.Search.Trim());
                // Postgres LIKE is case-sensitive; ILIKE keeps the old SQL Server (CI collation) behaviour in-database.
                query = query.Where(t =>
                    (t.Payee != null && EF.Functions.ILike(t.Payee, pattern, LikePattern.EscapeCharacter)) ||
                    (t.Notes != null && EF.Functions.ILike(t.Notes, pattern, LikePattern.EscapeCharacter)));
            }
        }

        return await query
            .OrderByDescending(t => t.OccurredAt)
            .ToListAsync();
    }

    public async Task<bool> SetNotesAsync(int id, string? notes, int userId)
    {
        var affected = await context.Transactions
            .Where(t => t.Id == id && t.Account.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Notes, notes));

        return affected > 0;
    }

    public async Task<IReadOnlyList<Transaction>> GetByIdsAsync(IEnumerable<int> ids, int userId)
    {
        var idList = ids.Distinct().ToList();
        return await context.Transactions
            .AsNoTracking()
            .Where(t => idList.Contains(t.Id) && t.Account.UserId == userId)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Transaction>> GetByPlaidTransactionIdsAsync(IEnumerable<string> plaidTransactionIds)
    {
        var ids = plaidTransactionIds.Distinct().ToList();
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.PlaidTransactionId != null && ids.Contains(t.PlaidTransactionId))
            .ToListAsync();
    }

    public async Task<int> SetCategoryAsync(IReadOnlyDictionary<int, string> transactionTypes, int? categoryId, int userId)
    {
        var ids = transactionTypes.Keys.ToList();
        if (ids.Count == 0)
            return 0;

        // Scoped through Account.UserId so ids belonging to someone else are silently excluded
        // rather than trusted — the same tenancy rule every other read here uses. Tracked rather than
        // ExecuteUpdate because each row gets its own type; one SaveChanges keeps the batch atomic.
        var rows = await context.Transactions
            .Where(t => ids.Contains(t.Id) && t.Account.UserId == userId)
            .ToListAsync();

        foreach (var row in rows)
        {
            row.CategoryId = categoryId;
            row.TransactionType = transactionTypes[row.Id];
        }

        await context.SaveChangesAsync();
        return rows.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> GetLatestCategoryByPayeeAsync(int userId)
    {
        // ponytail: pulls every categorized row's payee and picks in memory; fine for one user's ledger, a DISTINCT ON
        // query if it ever gets big.
        var rows = await context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId && t.CategoryId != null && t.Payee != null)
            .OrderByDescending(t => t.OccurredAt)
            .ThenByDescending(t => t.Id)
            .Select(t => new { Payee = t.Payee!, CategoryId = t.CategoryId!.Value })
            .ToListAsync();

        var latest = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
            latest.TryAdd(row.Payee, row.CategoryId);
        return latest;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Transaction>> GetTransferCandidatesAsync(int userId, IReadOnlyCollection<string> plaidCategoryPrimaries)
    {
        var categories = plaidCategoryPrimaries.ToList();
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId &&
                        !t.IsPending &&
                        t.PlaidCategoryPrimary != null &&
                        categories.Contains(t.PlaidCategoryPrimary))
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task ApplyTransferUpdatesAsync(IReadOnlyList<TransferUpdate> updates)
    {
        if (updates.Count == 0)
            return;

        var updateById = updates.ToDictionary(u => u.TransactionId);
        var ids = updateById.Keys.ToList();
        var rows = await context.Transactions.Where(t => ids.Contains(t.Id)).ToListAsync();

        // Type and transfer account change together: CK_Transactions_TransferAccount requires both or neither.
        foreach (var row in rows)
        {
            row.TransactionType = updateById[row.Id].TransactionType;
            row.TransferAccountId = updateById[row.Id].TransferAccountId;
        }

        await context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<(int Inserted, int Updated)> UpsertImportedAsync(IEnumerable<Transaction> transactions)
    {
        var incoming = transactions.ToList();
        if (incoming.Count == 0)
            return (0, 0);

        var ids = incoming
            .Select(t => t.PlaidTransactionId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var existing = await context.Transactions
            .Where(t => t.PlaidTransactionId != null && ids.Contains(t.PlaidTransactionId))
            .ToDictionaryAsync(t => t.PlaidTransactionId!, t => t);

        var inserted = 0;
        var updated = 0;

        foreach (var incomingTxn in incoming)
        {
            if (incomingTxn.PlaidTransactionId is null)
                continue;

            if (existing.TryGetValue(incomingTxn.PlaidTransactionId, out var current))
            {
                current.Amount = incomingTxn.Amount;
                current.OccurredAt = incomingTxn.OccurredAt;
                current.Payee = incomingTxn.Payee;

                // A paired Transfer keeps its type: overwriting it would also break CK_Transactions_TransferAccount,
                // which needs a Transfer to carry its TransferAccountId and anything else to carry none.
                if (current.TransactionType != TransferType)
                    current.TransactionType = incomingTxn.TransactionType;
                current.IsPending = incomingTxn.IsPending;
                current.PlaidAccountId = incomingTxn.PlaidAccountId;
                current.PlaidCategoryPrimary = incomingTxn.PlaidCategoryPrimary;

                // CategoryId is user-owned (BUD-9: "my choice sticks on future syncs"), so a re-sync
                // never overwrites it. Only backfill when the row is still uncategorized — that lets a
                // mapping added after the import take effect on the next sync.
                if (current.CategoryId is null)
                    current.CategoryId = incomingTxn.CategoryId;

                updated++;
            }
            else
            {
                context.Transactions.Add(incomingTxn);
                inserted++;
            }
        }

        await context.SaveChangesAsync();
        return (inserted, updated);
    }

    /// <inheritdoc />
    public async Task<int> DeleteByPlaidTransactionIdsAsync(IEnumerable<string> plaidTransactionIds)
    {
        var ids = plaidTransactionIds.Distinct().ToList();
        if (ids.Count == 0)
            return 0;

        var toRemove = await context.Transactions
            .Where(t => t.PlaidTransactionId != null && ids.Contains(t.PlaidTransactionId))
            .ToListAsync();

        if (toRemove.Count == 0)
            return 0;

        context.Transactions.RemoveRange(toRemove);
        await context.SaveChangesAsync();
        return toRemove.Count;
    }
}
