using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>
/// Encapsulates persistence for <see cref="PlaidItem"/> and <see cref="PlaidAccount"/>.
/// Owns access_token encryption/decryption so callers never handle raw tokens.
/// </summary>
public interface IPlaidItemAccessor
{
    /// <summary>Returns the currently active PlaidItem for the user, or null if none is linked.</summary>
    Task<PlaidItem?> GetActiveByUserIdAsync(int userId);

    /// <summary>Returns the decrypted access_token for an active PlaidItem, or null if no active item exists.</summary>
    Task<string?> GetActiveAccessTokenAsync(int userId);

    /// <summary>
    /// Returns the active PlaidItem (with its <see cref="PlaidItem.Accounts"/>) identified by Plaid's
    /// string <c>item_id</c> — the identifier webhook payloads carry — or null if none is active.
    /// </summary>
    Task<PlaidItem?> GetByPlaidItemIdAsync(string plaidItemId);

    /// <summary>Returns the decrypted access_token for the active item with the given Plaid <c>item_id</c>, or null.</summary>
    Task<string?> GetAccessTokenByPlaidItemIdAsync(string plaidItemId);

    /// <summary>Returns every active PlaidItem (with its <see cref="PlaidItem.Accounts"/>) for the backup sweep.</summary>
    Task<IReadOnlyList<PlaidItem>> GetAllActiveAsync();

    /// <summary>
    /// Atomically deactivates any existing active PlaidItem for the user and inserts the new one.
    /// Encrypts <paramref name="accessTokenPlaintext"/> before persisting.
    /// </summary>
    Task<int> ReplaceActiveAsync(
        int userId,
        string accessTokenPlaintext,
        string plaidItemId,
        string institutionId,
        string institutionName,
        DateTime? consentExpiresAt,
        IReadOnlyList<PlaidAccount> accounts);

    /// <summary>Updates the sync cursor and last-synced timestamp after a successful sync.</summary>
    Task UpdateSyncStateAsync(int plaidItemId, string nextCursor, DateTime lastSyncedAt);

    /// <summary>Soft-deletes the active PlaidItem for the user (IsActive = 0). Returns false if none was active.</summary>
    Task<bool> DeactivateActiveAsync(int userId);
}
