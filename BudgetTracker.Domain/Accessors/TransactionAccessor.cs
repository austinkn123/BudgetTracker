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
}
