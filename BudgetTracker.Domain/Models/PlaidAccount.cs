namespace BudgetTracker.Domain.Models;

/// <summary>
/// Represents a Plaid account (e.g., checking, credit card) nested under a <see cref="PlaidItem"/>.
/// Persisted so we can render the last-4 "mask" on imported transactions.
/// </summary>
public class PlaidAccount
{
    public int Id { get; set; }
    public int PlaidItemId { get; set; }

    /// <summary>Plaid's opaque account identifier (unique across all Plaid accounts globally).</summary>
    public string PlaidAccountId { get; set; } = string.Empty;

    public string? Mask { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? AccountSubtype { get; set; }

    public PlaidItem Item { get; set; } = null!;
}
