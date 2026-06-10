using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Domain.Engines;

/// <summary>
/// Pure business logic for translating Plaid responses into BudgetTracker domain objects.
/// Handles the sign-convention flip (Plaid positive = outflow → BudgetTracker negative Expense)
/// and resolves which BudgetTracker account a Plaid-sourced transaction belongs to.
/// </summary>
public class PlaidEngine : IPlaidEngine
{
    /// <inheritdoc />
    public Transaction MapToBudgetTrackerTransaction(PlaidTransactionDto dto, int accountId)
    {
        // Plaid convention: positive = money out, negative = money in.
        // BudgetTracker convention (BUD-18): negative = outflow Expense, positive = inflow Income.
        var invertedAmount = -dto.Amount;
        var transactionType = invertedAmount < 0 ? "Expense" : "Income";

        return new Transaction
        {
            AccountId = accountId,
            TransactionType = transactionType,
            Amount = invertedAmount,
            OccurredAt = dto.Date,
            Payee = !string.IsNullOrWhiteSpace(dto.MerchantName) ? dto.MerchantName : dto.Name,
            PlaidTransactionId = dto.TransactionId,
            PlaidAccountId = dto.AccountId,
            IsImported = true,
            IsPending = dto.Pending
        };
    }

    /// <inheritdoc />
    public int? ResolveBudgetTrackerAccountId(PlaidAccountDto plaidAccount, IEnumerable<Account> userAccounts)
    {
        var displayName = BuildDisplayName(plaidAccount, institutionName: null);
        var match = userAccounts.FirstOrDefault(a =>
            string.Equals(a.Name, displayName, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
            return match.Id;

        // Try fallback: any account name containing both the Plaid name and (if present) the mask suffix.
        var maskSuffix = string.IsNullOrEmpty(plaidAccount.Mask) ? null : $"••{plaidAccount.Mask}";
        var fallback = userAccounts.FirstOrDefault(a =>
            a.Name.Contains(plaidAccount.Name, StringComparison.OrdinalIgnoreCase) &&
            (maskSuffix is null || a.Name.Contains(maskSuffix, StringComparison.OrdinalIgnoreCase)));

        return fallback?.Id;
    }

    /// <inheritdoc />
    public Account BuildBudgetTrackerAccount(PlaidAccountDto plaidAccount, string institutionName, int userId)
    {
        return new Account
        {
            UserId = userId,
            Name = BuildDisplayName(plaidAccount, institutionName),
            AccountType = plaidAccount.Type
        };
    }

    private static string BuildDisplayName(PlaidAccountDto plaidAccount, string? institutionName)
    {
        var prefix = string.IsNullOrWhiteSpace(institutionName) ? string.Empty : $"{institutionName} - ";
        var maskSuffix = string.IsNullOrEmpty(plaidAccount.Mask) ? string.Empty : $" (••{plaidAccount.Mask})";
        return $"{prefix}{plaidAccount.Name}{maskSuffix}";
    }
}
