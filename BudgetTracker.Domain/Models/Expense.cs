namespace BudgetTracker.Domain.Models;

public class Expense
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string? Merchant { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
}
