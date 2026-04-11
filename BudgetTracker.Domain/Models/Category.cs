namespace BudgetTracker.Domain.Models;

public class Category
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryType { get; set; } = "Expense";

    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<BudgetPlanLine> BudgetPlanLines { get; set; } = [];
}
