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
        await context.SaveChangesAsync();
        return user.Id;
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
