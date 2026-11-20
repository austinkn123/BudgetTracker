using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>Persistence for a user's <see cref="CategoryRule"/>s (BUD-24). Every read and write is scoped to one user.</summary>
public interface ICategoryRuleAccessor
{
    /// <summary>The user's rules with their <see cref="CategoryRule.Category"/> loaded, oldest first.</summary>
    Task<IReadOnlyList<CategoryRule>> GetByUserIdAsync(int userId);

    /// <summary>The rule, or null when it doesn't exist or belongs to another user.</summary>
    Task<CategoryRule?> GetByIdForUserAsync(int id, int userId);

    /// <summary>The patterns of the user's rules that file into <paramref name="categoryId"/>.</summary>
    Task<IReadOnlyList<string>> GetPatternsForCategoryAsync(int categoryId, int userId);

    /// <summary>Inserts the rule and returns its id.</summary>
    /// <exception cref="Common.UniqueNameViolationException">The user already has a rule with this pattern and match type.</exception>
    Task<int> CreateAsync(CategoryRule rule);

    /// <summary>Saves pattern, match type and category. False when the rule doesn't exist for <see cref="CategoryRule.UserId"/>.</summary>
    /// <exception cref="Common.UniqueNameViolationException">Another of the user's rules already has this pattern and match type.</exception>
    Task<bool> UpdateAsync(CategoryRule rule);

    /// <summary>Deletes the rule. False when it doesn't exist or belongs to another user.</summary>
    Task<bool> DeleteAsync(int id, int userId);
}
