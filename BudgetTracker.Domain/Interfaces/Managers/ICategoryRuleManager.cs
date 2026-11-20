using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>A rule as the Rules list shows it, with how many rows it has filed.</summary>
public record CategoryRuleView(int Id, string Pattern, string MatchType, int CategoryId, string CategoryName, int MatchedCount);

/// <summary>The new rule's id, and how many existing uncategorized rows it filed.</summary>
public record CategoryRuleCreated(int Id, int AppliedCount);

/// <summary>Workflows for "always categorize this payee as" rules (BUD-24).</summary>
public interface ICategoryRuleManager
{
    /// <summary>The user's rules, oldest first, each with its matched count.</summary>
    Task<Result<IReadOnlyList<CategoryRuleView>>> GetAllAsync(int userId);

    /// <summary>How many uncategorized rows <paramref name="candidate"/> would file if created now. Validates like create.</summary>
    Task<Result<int>> PreviewAsync(CategoryRule candidate);

    /// <summary>Creates the rule; when <paramref name="applyToExisting"/>, files the uncategorized rows it wins.</summary>
    Task<Result<CategoryRuleCreated>> CreateAsync(CategoryRule rule, bool applyToExisting);

    /// <summary>Saves a rule's pattern, match type and category. Rows it already filed keep their category.</summary>
    Task<Result<bool>> UpdateAsync(CategoryRule rule);

    /// <summary>Deletes the rule. Rows it already filed keep their category.</summary>
    Task<Result<bool>> DeleteAsync(int id, int userId);
}
