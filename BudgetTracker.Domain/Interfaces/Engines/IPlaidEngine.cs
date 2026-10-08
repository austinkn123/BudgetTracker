using BudgetTracker.Domain.Common;
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
    /// <param name="categoryId">Category resolved from Plaid's suggestion, or null to leave it uncategorized.</param>
    Transaction MapToBudgetTrackerTransaction(PlaidTransactionDto dto, int accountId, int? categoryId = null);

    /// <summary>
    /// Resolve Plaid's suggested category (personal_finance_category.primary) to one of the user's
    /// categories, via the mapping stored on <see cref="Category.PlaidCategoryPrimary"/>.
    /// Returns null when Plaid sent no suggestion or the user has not mapped that value.
    /// </summary>
    int? ResolveCategoryId(PlaidTransactionDto dto, IEnumerable<Category> userCategories);

    /// <summary>
    /// Resolve which BudgetTracker <see cref="Account"/> should host transactions for a Plaid account.
    /// Returns the existing account id if a matching one exists for the user, else null (Manager will create one).
    /// </summary>
    int? ResolveBudgetTrackerAccountId(PlaidAccountDto plaidAccount, IEnumerable<Account> userAccounts);

    /// <summary>
    /// Build the default BudgetTracker <see cref="Account"/> for a newly-linked Plaid account (used when none exists).
    /// </summary>
    Account BuildBudgetTrackerAccount(PlaidAccountDto plaidAccount, string institutionName, int userId);

    /// <summary>
    /// Pick the items a sync run should refresh. With no threshold every item is due; otherwise only items
    /// never synced, or last synced strictly more than <paramref name="staleAfterHours"/> hours before <paramref name="now"/>.
    /// </summary>
    /// <param name="items">The user's active Plaid items.</param>
    /// <param name="now">Current time (UTC) supplied by the caller so the rule stays deterministic.</param>
    /// <param name="staleAfterHours">Staleness threshold in hours, or null to select every item.</param>
    /// <returns>The due items in input order, or a failure when the threshold is negative.</returns>
    Result<IReadOnlyList<PlaidItem>> SelectItemsDueForSync(IEnumerable<PlaidItem> items, DateTime now, int? staleAfterHours);

    /// <summary>
    /// Decide whether a just-exchanged Plaid item duplicates one of the user's active connections.
    /// An incoming item is a duplicate when any of its accounts shares a Plaid <c>account_id</c> with an active item,
    /// or when an active item at the same institution has an account with the same mask and name.
    /// A second login at the same institution with different accounts is not a duplicate.
    /// </summary>
    /// <param name="institutionId">Plaid institution id of the incoming item.</param>
    /// <param name="incomingAccounts">Accounts returned for the incoming item.</param>
    /// <param name="activeItems">The user's active items, with their account snapshots. Any item with IsActive false is ignored.</param>
    /// <returns>True when the incoming item is already linked.</returns>
    bool IsAlreadyLinked(string institutionId, IReadOnlyList<PlaidAccountDto> incomingAccounts, IEnumerable<PlaidItem> activeItems);
}
