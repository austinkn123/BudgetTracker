namespace BudgetTracker.Domain.Models;

/// <summary>One transaction as the CSV export writes it (BUD-25). <see cref="AccountName"/> already carries the mask.</summary>
public record TransactionExportRow(
    DateTime OccurredAt,
    string? Payee,
    decimal Amount,
    string TransactionType,
    string? CategoryName,
    string AccountName,
    bool IsPending,
    string? Notes,
    string? PlaidCategoryPrimary);
