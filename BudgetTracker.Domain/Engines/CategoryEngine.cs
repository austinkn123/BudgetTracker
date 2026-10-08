using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Engines;

public class CategoryEngine : ICategoryEngine
{
    private static readonly HashSet<string> ValidCategoryTypes = ["Income", "Expense", "Both"];

    public string? ValidateCategory(Category category)
    {
        if (category.UserId <= 0)
            return "A valid user is required";

        if (string.IsNullOrWhiteSpace(category.Name))
            return "Category name is required";

        if (category.Name.Length > 100)
            return "Category name must be 100 characters or fewer";

        if (string.IsNullOrWhiteSpace(category.CategoryType) || !ValidCategoryTypes.Contains(category.CategoryType))
            return "Category type must be Income, Expense, or Both";

        return null;
    }

    /// <inheritdoc />
    public string? ValidateNameIsUnique(string name, IEnumerable<string> existingNames)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(existingNames);

        return existingNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            ? DuplicateNameError(name)
            : null;
    }

    /// <inheritdoc />
    public string DuplicateNameError(string name) => $"A category named \"{name}\" already exists";
}
