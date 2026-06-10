using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>
/// Pure business logic for Plaid integration. No I/O. Owns the Plaid-to-BudgetTracker amount sign inversion
/// and the Plaid-to-BudgetTracker TransactionType mapping.
/// </summary>
public interface IPlaidEngine
{
    /// <summary>
    /// Map a raw Plaid transaction onto a BudgetTracker <see cref="Transaction"/> ready for persistence.
    /// Inverts Plaid's sign convention (Plaid positive = outflow → BudgetTracker negative Expense).
    /// </summary>
    /// <param name="dto">Plaid's raw transaction record.</param>
    /// <param name="accountId">BudgetTracker account id the imported transaction is attached to.</param>
    Transaction MapToBudgetTrackerTransaction(PlaidTransactionDto dto, int accountId);

    /// <summary>
    /// Resolve which BudgetTracker <see cref="Account"/> should host transactions for a Plaid account.
    /// Returns the existing account id if a matching one exists for the user, else null (Manager will create one).
    /// </summary>
    int? ResolveBudgetTrackerAccountId(PlaidAccountDto plaidAccount, IEnumerable<Account> userAccounts);

    /// <summary>
    /// Build the default BudgetTracker <see cref="Account"/> for a newly-linked Plaid account (used when none exists).
    /// </summary>
    Account BuildBudgetTrackerAccount(PlaidAccountDto plaidAccount, string institutionName, int userId);
}
