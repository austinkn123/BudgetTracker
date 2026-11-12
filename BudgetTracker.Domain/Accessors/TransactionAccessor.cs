using System.Linq.Expressions;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.Analysis;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

public class TransactionAccessor(BudgetTrackerDbContext context) : ITransactionAccessor
{
    private const string TransferType = "Transfer";
    private const string IncomeType = "Income";

    /// <summary>
    /// A row waiting on a category: none set, and not a Transfer (a paired Transfer never needs one). The single source
    /// for the summary's uncategorizedCount and the uncategorized filter; must match the client's needsCategory.
    /// </summary>
    private static readonly Expression<Func<Transaction, bool>> NeedsCategory =
        t => t.CategoryId == null && t.TransactionType != TransferType;

    /// <inheritdoc />
    public async Task<IEnumerable<Transaction>> GetFilteredAsync(int userId, TransactionFilter filter)
    {
        return await Filtered(userId, filter)
            .OrderByDescending(t => t.OccurredAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Transaction>> GetPageAsync(int userId, TransactionFilter filter, TransactionCursor? after, int take)
    {
        var query = Filtered(userId, filter);

        if (after is not null)
        {
            // Keyset seek on (OccurredAt DESC, Id DESC). The redundant OccurredAt <= bound is what makes the seek a range
            // on IX_Transactions_AccountId_OccurredAt; the OR alone would leave the planner nothing to range-scan on.
            var at = after.OccurredAt;
            var id = after.Id;
            query = query.Where(t => t.OccurredAt <= at && (t.OccurredAt < at || t.Id < id));
        }

        return await query
            .OrderByDescending(t => t.OccurredAt)
            .ThenByDescending(t => t.Id)
            .Take(take)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<TransactionSummary> GetSummaryAsync(int userId)
    {
        var owned = context.Transactions.AsNoTracking().Where(t => t.Account.UserId == userId);

        var uncategorized = await owned.CountAsync(NeedsCategory);

        var pending = await owned
            .Where(t => t.IsPending && t.PlaidAccountId != null)
            .GroupBy(t => t.PlaidAccountId!)
            .OrderBy(g => g.Key)
            .Select(g => new PendingAccountTotal(g.Key, g.Sum(t => t.Amount)))
            .ToListAsync();

        return new TransactionSummary(uncategorized, pending);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryUsage>> GetCategoryUsageAsync(int userId, DateTime since)
    {
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId && t.CategoryId != null)
            .GroupBy(t => t.CategoryId!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new CategoryUsage(
                g.Key,
                g.Count(),
                g.Sum(t => t.OccurredAt >= since && t.Amount > 0 ? t.Amount : 0m),
                g.Sum(t => t.OccurredAt >= since && t.Amount < 0 ? t.Amount : 0m)))
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryTypeTotal>> GetTotalsByCategoryAndTypeAsync(int userId, DateTime from, DateTime to)
    {
        // Same whole-day bounds as the filtered read, so the totals cover exactly the rows GetFilteredAsync would return.
        return await Filtered(userId, new TransactionFilter(From: from, To: to))
            .GroupBy(t => new { t.CategoryId, t.TransactionType })
            .OrderBy(g => g.Key.CategoryId)
            .ThenBy(g => g.Key.TransactionType)
            .Select(g => new CategoryTypeTotal(g.Key.CategoryId, g.Key.TransactionType, g.Sum(t => t.Amount)))
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int?>> GetCategoryIdsInUseAsync(int userId)
    {
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId)
            .Select(t => t.CategoryId)
            .Distinct()
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, decimal>> GetPostedSumByPlaidAccountAfterAsync(int userId, DateTime after)
    {
        if (after.Date == DateTime.MaxValue.Date)
            return new Dictionary<string, decimal>();

        var dayAfter = after.Date.AddDays(1);
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId && !t.IsPending && t.PlaidAccountId != null && t.OccurredAt >= dayAfter)
            .GroupBy(t => t.PlaidAccountId!)
            .Select(g => new { PlaidAccountId = g.Key, Amount = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.PlaidAccountId, x => x.Amount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Transaction>> GetNewestIncomeAsync(
        int userId, IReadOnlyCollection<int> categoryIds, DateTime onOrBefore, decimal? minAmount, int take)
    {
        var ids = categoryIds.Distinct().ToList();
        if (ids.Count == 0 || take <= 0)
            return [];

        var query = context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId && t.TransactionType == IncomeType &&
                        t.CategoryId != null && ids.Contains(t.CategoryId.Value));

        if (onOrBefore.Date < DateTime.MaxValue.Date)
        {
            var dayAfter = onOrBefore.Date.AddDays(1);
            query = query.Where(t => t.OccurredAt < dayAfter);
        }

        if (minAmount is decimal min)
            query = query.Where(t => t.Amount >= min);

        return await query
            .OrderByDescending(t => t.OccurredAt)
            .ThenByDescending(t => t.Id)
            .Take(take)
            .ToListAsync();
    }

    /// <summary>The user's rows narrowed by <paramref name="filter"/>, unordered and untracked.</summary>
    private IQueryable<Transaction> Filtered(int userId, TransactionFilter filter)
    {
        var query = context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId);

        // Compare on calendar days, matching BudgetAnalysisManager — otherwise the ledger and
        // the analysis disagree about which rows are in the month. The end bound is an
        // exclusive next-midnight rather than a CAST to date: it includes a row with any time
        // component, and it stays sargable so IX_Transactions_AccountId_OccurredAt is still used.
        if (filter.From is DateTime from)
        {
            var fromDate = from.Date;
            query = query.Where(t => t.OccurredAt >= fromDate);
        }

        // The last representable day has no next midnight, and nothing can be dated after it anyway.
        if (filter.To is DateTime to && to.Date < DateTime.MaxValue.Date)
        {
            var dayAfterTo = to.Date.AddDays(1);
            query = query.Where(t => t.OccurredAt < dayAfterTo);
        }

        // True is the review backlog ("needs a category"), so Transfers are left out; false is every categorized row.
        if (filter.Uncategorized is bool uncategorized)
            query = uncategorized
                ? query.Where(NeedsCategory)
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

        return query;
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
    public async Task<IReadOnlyDictionary<string, int>> GetLatestCategoryByPayeeAsync(int userId, IReadOnlyCollection<string> payees)
    {
        var keys = payees
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.ToLowerInvariant())
            .Distinct()
            .ToList();
        if (keys.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Only the batch's payees, newest pick per payee chosen in the database (a per-group LIMIT 1), so a sync reads
        // at most one row per payee instead of every categorized row the user has.
        var latest = await context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId && t.CategoryId != null && t.Payee != null && keys.Contains(t.Payee.ToLower()))
            .GroupBy(t => t.Payee!.ToLower())
            .Select(g => new
            {
                Payee = g.Key,
                CategoryId = g.OrderByDescending(t => t.OccurredAt).ThenByDescending(t => t.Id).Select(t => t.CategoryId!.Value).First()
            })
            .ToListAsync();

        return latest.ToDictionary(x => x.Payee, x => x.CategoryId, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Transaction>> GetTransferCandidatesAsync(int userId, IReadOnlyCollection<string> plaidCategoryPrimaries)
    {
        var categories = plaidCategoryPrimaries.ToList();

        // Pairing reads every candidate the user has, so only the columns it uses come back.
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId &&
                        !t.IsPending &&
                        t.PlaidCategoryPrimary != null &&
                        categories.Contains(t.PlaidCategoryPrimary))
            .Select(t => new Transaction
            {
                Id = t.Id,
                AccountId = t.AccountId,
                PlaidAccountId = t.PlaidAccountId,
                Amount = t.Amount,
                OccurredAt = t.OccurredAt,
                PlaidCategoryPrimary = t.PlaidCategoryPrimary,
                IsPending = t.IsPending,
                TransactionType = t.TransactionType,
                TransferAccountId = t.TransferAccountId,
                CategoryId = t.CategoryId
            })
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
