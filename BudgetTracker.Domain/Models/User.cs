namespace BudgetTracker.Domain.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<Category> Categories { get; set; } = [];
    public ICollection<Account> Accounts { get; set; } = [];
}
