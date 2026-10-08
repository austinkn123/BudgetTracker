using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>
/// Encapsulates persistence for <see cref="PlaidItem"/> and <see cref="PlaidAccount"/>.
/// Owns access_token encryption/decryption so callers never handle raw tokens.
/// </summary>
public interface IPlaidItemAccessor
{
    /// <summary>
    /// Returns every active PlaidItem (with its <see cref="PlaidItem.Accounts"/>) for the user, ordered by id.
    /// Empty when the user has no linked banks.
    /// </summary>
    Task<IReadOnlyList<PlaidItem>> GetAllActiveByUserIdAsync(int userId);

    /// <summary>
    /// Reads and decrypts the access_token for the user's active PlaidItem with the given row id.
    /// Returns <see cref="AccessTokenStatus.NotFound"/> when the item does not exist, belongs to another user, or is
    /// inactive, and <see cref="AccessTokenStatus.Undecryptable"/> when the stored token cannot be decrypted. Never throws
    /// for a decryption failure.
    /// </summary>
    Task<AccessTokenLookup> GetActiveAccessTokenAsync(int userId, int plaidItemId);

    /// <summary>
    /// Returns the active PlaidItem (with its <see cref="PlaidItem.Accounts"/>) identified by Plaid's
    /// string <c>item_id</c> — the identifier webhook payloads carry — or null if none is active.
    /// </summary>
    Task<PlaidItem?> GetByPlaidItemIdAsync(string plaidItemId);

    /// <summary>
    /// Reads and decrypts the access_token for the active item with the given Plaid <c>item_id</c>.
    /// Returns <see cref="AccessTokenStatus.NotFound"/> or <see cref="AccessTokenStatus.Undecryptable"/> instead of throwing.
    /// </summary>
    Task<AccessTokenLookup> GetAccessTokenByPlaidItemIdAsync(string plaidItemId);

    /// <summary>Returns every active PlaidItem (with its <see cref="PlaidItem.Accounts"/>) for the backup sweep.</summary>
    Task<IReadOnlyList<PlaidItem>> GetAllActiveAsync();

    /// <summary>
    /// Inserts a new active PlaidItem for the user alongside any existing ones.
    /// Encrypts <paramref name="accessTokenPlaintext"/> before persisting. Returns the new row id.
    /// </summary>
    Task<int> AddAsync(
        int userId,
        string accessTokenPlaintext,
        string plaidItemId,
        string institutionId,
        string institutionName,
        DateTime? consentExpiresAt,
        IReadOnlyList<PlaidAccount> accounts);

    /// <summary>Updates the sync cursor and last-synced timestamp after a successful sync.</summary>
    Task UpdateSyncStateAsync(int plaidItemId, string nextCursor, DateTime lastSyncedAt);

    /// <summary>
    /// Soft-deletes one of the user's active PlaidItems (IsActive = false). Returns false when the item does not
    /// exist, belongs to another user, or is already inactive.
    /// </summary>
    Task<bool> DeactivateAsync(int userId, int plaidItemId);

    /// <summary>
    /// Undoes <see cref="DeactivateAsync"/> for one of the user's inactive PlaidItems (IsActive = true). Returns false when
    /// the item does not exist, belongs to another user, or is already active.
    /// </summary>
    /// <param name="userId">The owning user.</param>
    /// <param name="plaidItemId">The PlaidItem row id.</param>
    /// <returns>True when the item was reactivated.</returns>
    Task<bool> ReactivateAsync(int userId, int plaidItemId);
}
