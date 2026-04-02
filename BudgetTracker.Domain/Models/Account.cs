namespace BudgetTracker.Domain.Models;

public class Account
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = [];
}
