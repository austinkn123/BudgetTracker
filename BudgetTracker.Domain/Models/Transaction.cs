namespace BudgetTracker.Domain.Models;

public class Transaction
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int? CategoryId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Payee { get; set; }
    public string? Notes { get; set; }
    public int? TransferAccountId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Account Account { get; set; } = null!;
    public Category? Category { get; set; }
    public Account? TransferAccount { get; set; }
}
