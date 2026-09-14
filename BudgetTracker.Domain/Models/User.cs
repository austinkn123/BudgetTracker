namespace BudgetTracker.Domain.Models;

public class User
{
    public int Id { get; set; }

    /// <summary>
    /// Cognito's subject claim — the sole identity linkage. Cognito owns the email address and
    /// every other profile attribute; this row exists only to key budget data to a login.
    /// </summary>
    public string? CognitoSub { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<Category> Categories { get; set; } = [];
    public ICollection<Account> Accounts { get; set; } = [];
    public ICollection<BudgetPlan> BudgetPlans { get; set; } = [];
}
