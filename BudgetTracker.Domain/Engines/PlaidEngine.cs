using BudgetTracker.Domain.Common;
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
    public Transaction MapToBudgetTrackerTransaction(PlaidTransactionDto dto, int accountId, int? categoryId = null)
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
            PlaidCategoryPrimary = dto.PersonalFinanceCategoryPrimary,
            CategoryId = categoryId,
            IsImported = true,
            IsPending = dto.Pending
        };
    }

    /// <inheritdoc />
    public int? ResolveCategoryId(PlaidTransactionDto dto, IEnumerable<Category> userCategories)
    {
        if (string.IsNullOrWhiteSpace(dto.PersonalFinanceCategoryPrimary))
            return null;

        var match = userCategories.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(c.PlaidCategoryPrimary) &&
            string.Equals(c.PlaidCategoryPrimary, dto.PersonalFinanceCategoryPrimary, StringComparison.OrdinalIgnoreCase));

        return match?.Id;
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
    public bool IsAlreadyLinked(string institutionId, IReadOnlyList<PlaidAccountDto> incomingAccounts, IEnumerable<PlaidItem> activeItems)
    {
        ArgumentNullException.ThrowIfNull(incomingAccounts);
        ArgumentNullException.ThrowIfNull(activeItems);

        return activeItems.Where(item => item.IsActive).Any(item => item.Accounts.Any(existing => incomingAccounts.Any(incoming =>
            existing.PlaidAccountId == incoming.AccountId ||
            (item.InstitutionId == institutionId && IsSameAccountByMaskAndName(existing, incoming)))));
    }

    /// <summary>A null mask is treated as unknown, never as a match, so generic names alone cannot block a link.</summary>
    private static bool IsSameAccountByMaskAndName(PlaidAccount existing, PlaidAccountDto incoming) =>
        existing.Mask is not null &&
        existing.Mask == incoming.Mask &&
        string.Equals(existing.Name, incoming.Name, StringComparison.OrdinalIgnoreCase);

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

    public Result<IReadOnlyList<PlaidItem>> SelectItemsDueForSync(IEnumerable<PlaidItem> items, DateTime now, int? staleAfterHours)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (staleAfterHours is < 0)
            return Result<IReadOnlyList<PlaidItem>>.Failure("staleAfterHours cannot be negative");

        if (staleAfterHours is not int hours)
            return Result<IReadOnlyList<PlaidItem>>.Success(items.ToList());

        var cutoff = now.AddHours(-hours);
        return Result<IReadOnlyList<PlaidItem>>.Success(
            items.Where(i => i.LastSyncedAt is null || i.LastSyncedAt < cutoff).ToList());
    }
}
