using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

public class TransactionAccessor(BudgetTrackerDbContext context) : ITransactionAccessor
{
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

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                // EF.Functions.Like keeps the match in SQL Server rather than pulling rows to compare.
                query = query.Where(t =>
                    (t.Payee != null && EF.Functions.Like(t.Payee, $"%{term}%")) ||
                    (t.Notes != null && EF.Functions.Like(t.Notes, $"%{term}%")));
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

    public async Task<int> SetCategoryAsync(IEnumerable<int> ids, int? categoryId, int userId)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return 0;

        // Scoped through Account.UserId so ids belonging to someone else are silently excluded
        // rather than trusted — the same tenancy rule every other read here uses.
        return await context.Transactions
            .Where(t => idList.Contains(t.Id) && t.Account.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.CategoryId, categoryId));
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
