namespace BudgetTracker.Domain.Models;

/// <summary>
/// Server-side filter for the transactions ledger. Every field is optional; a null field means
/// "no constraint". Pushed into the EF query so the client never pages a whole ledger into memory
/// just to show one month or the uncategorized subset.
/// </summary>
public record TransactionFilter(
    DateTime? From = null,
    DateTime? To = null,
    /// <summary>True = only rows with no category. False = only categorised rows.</summary>
    bool? Uncategorized = null,
    /// <summary>True = only Plaid-imported rows. False = only manual entries.</summary>
    bool? IsImported = null,
    bool? IsPending = null,
    /// <summary>Case-insensitive substring match against payee and notes.</summary>
    string? Search = null);
