namespace BudgetTracker.Domain.Models;

/// <summary>
/// A change own-account transfer pairing makes to one row: a paired leg becomes a Transfer to its counterpart's
/// account; a leg that no longer pairs goes back to Income/Expense with no <paramref name="TransferAccountId"/>.
/// </summary>
public record TransferUpdate(int TransactionId, string TransactionType, int? TransferAccountId);
