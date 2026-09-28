namespace BudgetTracker.Domain.Models;

public class Category
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryType { get; set; } = "Expense";

    /// <summary>
    /// Optional Plaid taxonomy value (personal_finance_category.primary) this category claims.
    /// When set, imported transactions carrying that value resolve to this category automatically.
    /// </summary>
    public string? PlaidCategoryPrimary { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<BudgetPlanEntry> BudgetPlanEntries { get; set; } = [];
}
