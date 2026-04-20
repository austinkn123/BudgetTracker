using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

public class CategoryAccessor(BudgetTrackerDbContext context) : ICategoryAccessor
{
    public async Task<int> CreateAsync(Category category)
    {
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        var category = await context.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (category is null) return false;

        context.Categories.Remove(category);
        return await context.SaveChangesAsync() > 0;
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        return await context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Category?> GetByIdForUserAsync(int id, int userId)
    {
        return await context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
    }

    public async Task<IEnumerable<Category>> GetByUserIdAsync(int userId)
    {
        return await context.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .ToListAsync();
    }

    public async Task<bool> UpdateAsync(Category category)
    {
        context.Categories.Update(category);
        return await context.SaveChangesAsync() > 0;
    }
}
