namespace BudgetTracker.Domain.Models;

public class BudgetPlanLine
{
    public int Id { get; set; }
    public int BudgetPlanId { get; set; }
    public int? CategoryId { get; set; }
    public string LineType { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string Cadence { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal MonthlyEquivalent { get; set; }
    public bool IsStressFactor { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public BudgetPlan BudgetPlan { get; set; } = null!;
    public Category? Category { get; set; }
}
