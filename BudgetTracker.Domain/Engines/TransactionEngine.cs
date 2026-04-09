using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Engines;

public class TransactionEngine : ITransactionEngine
{
    private static readonly HashSet<string> ValidTransactionTypes =
    [
        "Expense",
        "Income",
        "Transfer",
        "Adjustment"
    ];

    public string? ValidateTransaction(Transaction transaction)
    {
        if (transaction.AccountId <= 0)
            return "A valid account is required";

        if (!string.IsNullOrWhiteSpace(transaction.TransactionType) &&
            !ValidTransactionTypes.Contains(transaction.TransactionType))
            return "Transaction type must be one of: Expense, Income, Transfer, Adjustment";

        if (transaction.Amount <= 0)
            return "Amount must be greater than zero";

        if (transaction.OccurredAt > DateTime.UtcNow)
            return "Transaction date cannot be in the future";

        if (transaction.CategoryId is <= 0)
            return "Category ID must be greater than zero when provided";

        if (transaction.TransactionType == "Transfer" && transaction.TransferAccountId is null)
            return "Transfer transactions require a destination account";

        if (transaction.TransactionType != "Transfer" && transaction.TransferAccountId is not null)
            return "Only transfer transactions can specify a destination account";

        return null;
    }
}
