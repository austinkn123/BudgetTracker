using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

/// <summary>
/// EF Core-backed implementation of <see cref="IAccountAccessor"/>.
/// </summary>
public class AccountAccessor(BudgetTrackerDbContext context) : IAccountAccessor
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Account>> GetByUserIdAsync(int userId)
    {
        return await context.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<int> CreateAsync(Account account)
    {
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }
}
