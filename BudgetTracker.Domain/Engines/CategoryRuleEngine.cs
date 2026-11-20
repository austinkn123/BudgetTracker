using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Engines;

/// <inheritdoc />
public class CategoryRuleEngine : ICategoryRuleEngine
{
    private const int MaxPatternLength = 255;

    /// <inheritdoc />
    public string NormalizePattern(string? pattern) => pattern?.Trim() ?? string.Empty;

    /// <inheritdoc />
    public string? ValidateRule(CategoryRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Pattern))
            return "Pattern is required";
        if (rule.Pattern.Length > MaxPatternLength)
            return $"Pattern must be {MaxPatternLength} characters or fewer";
        if (!CategoryRuleMatchType.All.Contains(rule.MatchType))
            return "Match type must be Contains or Exact";
        if (rule.CategoryId <= 0)
            return "Category is required";
        return null;
    }

    /// <inheritdoc />
    public bool Matches(CategoryRule rule, string? payee)
    {
        if (string.IsNullOrWhiteSpace(payee) || string.IsNullOrWhiteSpace(rule.Pattern))
            return false;

        return rule.MatchType == CategoryRuleMatchType.Exact
            ? string.Equals(payee.Trim(), rule.Pattern.Trim(), StringComparison.OrdinalIgnoreCase)
            : payee.Contains(rule.Pattern.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public CategoryRule? FindMatch(string? payee, IReadOnlyCollection<CategoryRule> rules) =>
        rules.Where(r => Matches(r, payee))
            .OrderByDescending(r => r.Pattern.Trim().Length)
            .ThenByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault();

    /// <inheritdoc />
    public IReadOnlyList<string> PayeesWonBy(CategoryRule rule, IReadOnlyCollection<CategoryRule> allRules, IEnumerable<string> payees) =>
        payees.Where(p => ReferenceEquals(FindMatch(p, allRules), rule)).ToList();

    /// <inheritdoc />
    public int CountMatched(CategoryRule rule, IReadOnlyCollection<CategoryRule> allRules, IEnumerable<PayeeCategoryCount> counts) =>
        counts.Where(c => c.CategoryId == rule.CategoryId && ReferenceEquals(FindMatch(c.Payee, allRules), rule)).Sum(c => c.Count);

    /// <inheritdoc />
    public int CountUncategorizedWon(CategoryRule rule, IReadOnlyCollection<CategoryRule> allRules, IEnumerable<PayeeCategoryCount> counts) =>
        counts.Where(c => c.CategoryId is null && ReferenceEquals(FindMatch(c.Payee, allRules), rule)).Sum(c => c.Count);

    /// <inheritdoc />
    public string DuplicateRuleError(string pattern) => $"A rule for \"{pattern}\" already exists";

    /// <inheritdoc />
    public string CategoryInUseByRulesError(IReadOnlyCollection<string> patterns) =>
        $"This category is used by rules: {string.Join(", ", patterns.Select(p => $"\"{p}\""))}. Delete or change those rules first.";
}
