namespace BudgetTracker.Domain.Models;

public class BudgetPlan
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime PlanMonth { get; set; }
    public decimal NetIncomeMonthly { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<BudgetPlanLine> Lines { get; set; } = [];
}
