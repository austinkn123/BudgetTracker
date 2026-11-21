using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

/// <inheritdoc />
public class CategoryRuleAccessor(BudgetTrackerDbContext context) : ICategoryRuleAccessor
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryRule>> GetByUserIdAsync(int userId) =>
        await context.CategoryRules
            .AsNoTracking()
            .Include(r => r.Category)
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Id)
            .ToListAsync();

    /// <inheritdoc />
    public Task<CategoryRule?> GetByIdForUserAsync(int id, int userId) =>
        context.CategoryRules
            .AsNoTracking()
            .Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetPatternsForCategoryAsync(int categoryId, int userId) =>
        await context.CategoryRules
            .AsNoTracking()
            .Where(r => r.CategoryId == categoryId && r.UserId == userId)
            .OrderBy(r => r.Id)
            .Select(r => r.Pattern)
            .ToListAsync();

    /// <inheritdoc />
    public async Task<int> CreateAsync(CategoryRule rule)
    {
        context.CategoryRules.Add(rule);
        await SaveTranslatingPatternViolationAsync();
        return rule.Id;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(CategoryRule rule)
    {
        var existing = await context.CategoryRules.FirstOrDefaultAsync(r => r.Id == rule.Id && r.UserId == rule.UserId);
        if (existing is null)
            return false;

        existing.Pattern = rule.Pattern;
        existing.MatchType = rule.MatchType;
        existing.CategoryId = rule.CategoryId;
        await SaveTranslatingPatternViolationAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id, int userId) =>
        await context.CategoryRules.Where(r => r.Id == id && r.UserId == userId).ExecuteDeleteAsync() > 0;

    private async Task SaveTranslatingPatternViolationAsync()
    {
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolationOf(UniqueViolation.CategoryRulePattern))
        {
            throw new UniqueNameViolationException(UniqueViolation.CategoryRulePattern, ex);
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolationOf(ForeignKeyViolation.CategoryRuleCategory))
        {
            // The category was deleted after the manager checked it was the user's.
            throw new MissingReferenceException(ForeignKeyViolation.CategoryRuleCategory, ex);
        }
    }
}
