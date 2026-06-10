using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

public class TransactionAccessor(BudgetTrackerDbContext context) : ITransactionAccessor
{
    public async Task<Transaction?> GetByIdAsync(int id, int userId)
    {
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Where(t => t.Account.UserId == userId)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Transaction>> GetByUserIdAsync(int userId)
    {
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId)
            .OrderByDescending(t => t.OccurredAt)
            .ToListAsync();
    }

    public async Task<bool> AccountBelongsToUserAsync(int accountId, int userId)
    {
        return await context.Accounts
            .AsNoTracking()
            .AnyAsync(a => a.Id == accountId && a.UserId == userId);
    }

    public async Task<int> CreateAsync(Transaction transaction)
    {
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();
        return transaction.Id;
    }

    public async Task<bool> UpdateAsync(Transaction transaction, int userId)
    {
        var existing = await context.Transactions
            .Where(t => t.Id == transaction.Id)
            .Where(t => t.Account.UserId == userId)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            return false;
        }

        existing.AccountId = transaction.AccountId;
        existing.CategoryId = transaction.CategoryId;
        existing.TransactionType = transaction.TransactionType;
        existing.Amount = transaction.Amount;
        existing.OccurredAt = transaction.OccurredAt;
        existing.Payee = transaction.Payee;
        existing.Notes = transaction.Notes;
        existing.TransferAccountId = transaction.TransferAccountId;

        return await context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        var transaction = await context.Transactions
            .Where(t => t.Id == id)
            .Where(t => t.Account.UserId == userId)
            .FirstOrDefaultAsync();

        if (transaction is null)
        {
            return false;
        }

        context.Transactions.Remove(transaction);
        return await context.SaveChangesAsync() > 0;
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
