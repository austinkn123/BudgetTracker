using BudgetTracker.Domain.Plaid;
using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>
/// Persists the user's Webull link: the encrypted access token and its status, the last balance and positions snapshot,
/// the last error, and the per-day value history. The snapshot (WebullAccounts, WebullPositions) is replaced on each
/// fetch. History (WebullDailyValues) is keyed by connection and AccountId, with no foreign key to WebullAccounts, so
/// replacing the snapshot never deletes history. Phase 3's trade tables follow the same keying under their own accessor. Owns token encryption via ASP.NET Core Data Protection (same mechanism as Plaid's <c>AccessTokenEncrypted</c>),
/// so callers never store a plaintext token. One record per user; every write creates it when missing.
/// Never touches Transactions, Accounts, Plaid tables, or budget data.
/// </summary>
public interface IWebullConnectionAccessor
{
    /// <summary>What is stored for the user, without the token; null when nothing has been stored.</summary>
    Task<WebullSnapshot?> GetAsync(int userId);

    /// <summary>
    /// Reads and decrypts the stored token. <see cref="AccessTokenStatus.NotFound"/> when none is stored,
    /// <see cref="AccessTokenStatus.Undecryptable"/> when the Data Protection key ring that encrypted it is gone.
    /// Never throws for a decryption failure.
    /// </summary>
    Task<AccessTokenLookup> GetAccessTokenAsync(int userId);

    /// <summary>
    /// Store a newly created token (encrypted) with its status and creation time, replacing any previous token, and clear
    /// the last error. Keeps the balance snapshot and its fetch time.
    /// </summary>
    Task SaveTokenAsync(int userId, string accessTokenPlaintext, WebullTokenStatus status, DateTime createdAt);

    /// <summary>
    /// Update the stored token's status after a successful Check Token call, and clear the last error.
    /// No-op when no token is stored.
    /// </summary>
    Task SetTokenStatusAsync(int userId, WebullTokenStatus status);

    /// <summary>Record why the most recent Webull call failed. Leaves the token, its status, and the balances untouched.</summary>
    Task SetLastErrorAsync(int userId, WebullErrorKind error);

    /// <summary>
    /// Save one successful fetch in a single <c>SaveChanges</c>, so it all lands or none of it does:
    /// <list type="bullet">
    /// <item>The balance snapshot is replaced: accounts are merged by AccountId and unlisted ones are dropped, with their
    /// positions. Each account's balance and three totals are written.</item>
    /// <item>Each account's positions are replaced wholesale by <see cref="WebullAccountPortfolio.Positions"/>. An empty
    /// list leaves none.</item>
    /// <item>Fetch time is set to <paramref name="fetchedAt"/> and the last error is cleared.</item>
    /// <item>One daily value record per account is upserted for (connection, AccountId, <paramref name="easternDate"/>).
    /// The last save of the day wins. Records for other dates, and for accounts no longer listed, are never touched.</item>
    /// </list>
    /// </summary>
    /// <param name="userId">The owner.</param>
    /// <param name="accounts">The fetch, from <see cref="IWebullAccessor.GetPortfolioAsync"/>.</param>
    /// <param name="fetchedAt">When it was fetched (UTC).</param>
    /// <param name="easternDate"><see cref="Engines.IWebullEngine.ToEasternDate"/> of <paramref name="fetchedAt"/>.</param>
    Task SavePortfolioAsync(int userId, IReadOnlyList<WebullAccountPortfolio> accounts, DateTime fetchedAt, DateOnly easternDate);

    /// <summary>
    /// The saved accounts with their totals and positions, in the same order as <see cref="WebullSnapshot.Balances"/>.
    /// Empty when nothing is saved. Fetch time and last error come from <see cref="GetAsync"/>.
    /// </summary>
    Task<IReadOnlyList<WebullAccountPortfolio>> GetSavedPortfolioAsync(int userId);

    /// <summary>Every user with a stored token, in any status. The scheduled close run iterates these users.</summary>
    Task<IReadOnlyList<int>> GetUserIdsWithTokenAsync();
}
