using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

public interface ICategoryAccessor
{
    Task<Category?> GetByIdAsync(int id);
    Task<Category?> GetByIdForUserAsync(int id, int userId);
    Task<IEnumerable<Category>> GetByUserIdAsync(int userId);

    /// <summary>Inserts <paramref name="category"/> and returns its new id.</summary>
    /// <param name="category">The category to insert.</param>
    /// <returns>The new category id.</returns>
    /// <exception cref="UniqueNameViolationException">The user already has a category with this name, ignoring case.</exception>
    Task<int> CreateAsync(Category category);

    /// <summary>Names of the user's categories other than <paramref name="excludeCategoryId"/>.</summary>
    /// <param name="userId">The owning user.</param>
    /// <param name="excludeCategoryId">The category being updated, or 0 when creating.</param>
    /// <returns>The other category names.</returns>
    Task<IReadOnlyList<string>> GetOtherNamesAsync(int userId, int excludeCategoryId);

    /// <summary>Saves changes to <paramref name="category"/>.</summary>
    /// <param name="category">The category to save.</param>
    /// <returns>True when a row was written.</returns>
    /// <exception cref="UniqueNameViolationException">The user already has a category with this name, ignoring case.</exception>
    Task<bool> UpdateAsync(Category category);
    Task<bool> DeleteAsync(int id, int userId);
}
