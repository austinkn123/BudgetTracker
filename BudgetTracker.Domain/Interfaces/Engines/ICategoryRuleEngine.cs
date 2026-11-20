using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>
/// Pure logic for "always categorize this payee as" rules (BUD-24): validation, matching a payee, and which rule wins
/// when several match.
/// </summary>
public interface ICategoryRuleEngine
{
    /// <summary>The pattern as stored: trimmed, or empty when null.</summary>
    string NormalizePattern(string? pattern);

    /// <summary>An error message when the rule is invalid; otherwise null. Expects an already-normalized pattern.</summary>
    string? ValidateRule(CategoryRule rule);

    /// <summary>Whether the rule matches the payee, ignoring case. Contains matches anywhere; Exact matches the whole (trimmed) payee.</summary>
    bool Matches(CategoryRule rule, string? payee);

    /// <summary>
    /// The rule that files <paramref name="payee"/>: of the matching rules, the longest pattern wins, then the newest
    /// (<see cref="CategoryRule.CreatedAt"/>, then the higher id). Null when none match.
    /// </summary>
    CategoryRule? FindMatch(string? payee, IReadOnlyCollection<CategoryRule> rules);

    /// <summary>The payees, out of <paramref name="payees"/>, whose winning rule among <paramref name="allRules"/> is <paramref name="rule"/>.</summary>
    IReadOnlyList<string> PayeesWonBy(CategoryRule rule, IReadOnlyCollection<CategoryRule> allRules, IEnumerable<string> payees);

    /// <summary>Rows already in the rule's category whose payee the rule wins: the "matched" figure shown per rule.</summary>
    int CountMatched(CategoryRule rule, IReadOnlyCollection<CategoryRule> allRules, IEnumerable<PayeeCategoryCount> counts);

    /// <summary>Uncategorized rows whose payee the rule wins: what "also apply to existing" would file.</summary>
    int CountUncategorizedWon(CategoryRule rule, IReadOnlyCollection<CategoryRule> allRules, IEnumerable<PayeeCategoryCount> counts);

    /// <summary>The message shown when a rule with this pattern and match type already exists.</summary>
    string DuplicateRuleError(string pattern);

    /// <summary>The message shown when a category can't be deleted because rules file into it.</summary>
    string CategoryInUseByRulesError(IReadOnlyCollection<string> patterns);
}
