using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

public class UserAccessor(BudgetTrackerDbContext context) : IUserAccessor
{
    public async Task<int> CreateAsync(User user)
    {
        context.Users.Add(user);
        try
        {
            await context.SaveChangesAsync();
            return user.Id;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolationOf(UniqueViolation.UserCognitoSub))
        {
            // Lost a first-login race: a concurrent request provisioned this sub first, so return that row.
            context.Entry(user).State = EntityState.Detached;
            var existing = await GetByCognitoSubAsync(user.CognitoSub!);
            return existing?.Id ?? throw ex;
        }
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByCognitoSubAsync(string sub)
    {
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.CognitoSub == sub);
    }
}
