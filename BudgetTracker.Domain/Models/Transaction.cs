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

    /// <summary>Plaid's transaction_id when this row was imported via Plaid; null for manual entries. Dedup key.</summary>
    public string? PlaidTransactionId { get; set; }

    /// <summary>Plaid's account_id (raw external id) when imported via Plaid; null for manual entries.</summary>
    public string? PlaidAccountId { get; set; }

    /// <summary>True for transactions sourced from Plaid sync. Used to lock down merchant/amount/date edits.</summary>
    public bool IsImported { get; set; }

    /// <summary>True while Plaid still reports the transaction as pending. Settles to false on the next sync after posting.</summary>
    public bool IsPending { get; set; }

    public Account Account { get; set; } = null!;
    public Category? Category { get; set; }
    public Account? TransferAccount { get; set; }
}
