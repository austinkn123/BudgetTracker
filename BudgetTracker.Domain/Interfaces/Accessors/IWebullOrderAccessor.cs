using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>
/// Persists the user's Webull order history (WebullOrders) and each account's sync progress (WebullOrderSyncs). Both are
/// history: keyed by connection and AccountId with no foreign key to WebullAccounts, so replacing the balance snapshot never
/// deletes them. Insert-or-update only; nothing here ever deletes an order. Looks the connection up by user. Never touches
/// Transactions, Accounts, Plaid tables or budget data.
/// </summary>
public interface IWebullOrderAccessor
{
    /// <summary>Every account's sync progress for the user, ordered by AccountId; empty when none is saved or no connection exists.</summary>
    Task<IReadOnlyList<WebullOrderSync>> GetSyncsAsync(int userId);

    /// <summary>
    /// Every saved order for the user, all accounts and statuses, ordered by AccountId then OrderId; empty when none.
    /// Timestamps come back with kind UTC. A few thousand rows at most, read in one query.
    /// </summary>
    Task<IReadOnlyList<WebullOrder>> GetOrdersAsync(int userId);

    /// <summary>
    /// Save one page in a single <c>SaveChanges</c>, so the orders and the progress land together or not at all:
    /// <list type="bullet">
    /// <item>Each order is inserted, or overwrites the saved row with the same (connection, AccountId, OrderId), so a later
    /// status or fill wins. Duplicates within <paramref name="orders"/> collapse, last one wins.</item>
    /// <item>The sync row for <see cref="WebullOrderSync.AccountId"/> is created or overwritten with <paramref name="sync"/>.</item>
    /// </list>
    /// An empty <paramref name="orders"/> saves only the progress (a failed fetch). On a failed save, nothing tracked is kept
    /// and the exception is rethrown.
    /// </summary>
    /// <exception cref="ArgumentException">An order's AccountId differs from <paramref name="sync"/>'s.</exception>
    /// <exception cref="InvalidOperationException">The user has no Webull connection.</exception>
    Task SavePageAsync(int userId, WebullOrderSync sync, IReadOnlyList<WebullOrder> orders);
}
