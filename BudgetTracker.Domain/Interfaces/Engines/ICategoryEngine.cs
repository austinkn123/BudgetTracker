using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Engines;

public interface ICategoryEngine
{
    string? ValidateCategory(Category category);

    /// <summary>
    /// Checks that <paramref name="name"/> does not match any of the user's other category names, ignoring case,
    /// mirroring the unique index on ("UserId", lower("Name")).
    /// </summary>
    /// <param name="name">The candidate category name.</param>
    /// <param name="existingNames">Names of the user's other categories (excluding the one being updated).</param>
    /// <returns>An error message when the name is taken; otherwise null.</returns>
    string? ValidateNameIsUnique(string name, IEnumerable<string> existingNames);

    /// <summary>The message shown when <paramref name="name"/> is already taken, used when the database catches a race.</summary>
    /// <param name="name">The candidate category name.</param>
    /// <returns>The same message <see cref="ValidateNameIsUnique"/> returns for a taken name.</returns>
    string DuplicateNameError(string name);
}
