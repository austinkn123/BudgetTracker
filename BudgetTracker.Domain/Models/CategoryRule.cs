namespace BudgetTracker.Domain.Models;

/// <summary>
/// "Always put this payee in this category" (BUD-24). Applied by sync to new, still-uncategorized imports, ahead of
/// the learned payee pick and Plaid's suggestion; it never overwrites a category already on a row.
/// </summary>
public class CategoryRule
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary><see cref="CategoryRuleMatchType.Contains"/> or <see cref="CategoryRuleMatchType.Exact"/>.</summary>
    public string MatchType { get; set; } = CategoryRuleMatchType.Contains;

    /// <summary>Compared case-insensitively against the payee shown on the row.</summary>
    public string Pattern { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
}

/// <summary>The ways a <see cref="CategoryRule"/> pattern can match a payee.</summary>
public static class CategoryRuleMatchType
{
    public const string Contains = "Contains";
    public const string Exact = "Exact";

    public static readonly IReadOnlyList<string> All = [Contains, Exact];
}
