using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>
/// Encapsulates HTTP calls to the Plaid API. Returns plain DTOs — never persists, never decides business outcomes.
/// </summary>
public interface IPlaidAccessor
{
    /// <summary>Create a Link token for the given Cognito-sub user identity.</summary>
    Task<PlaidLinkTokenResult> CreateLinkTokenAsync(string clientUserId, CancellationToken cancellationToken = default);

    /// <summary>Exchange a Link public_token for a long-lived access_token.</summary>
    Task<PlaidExchangeResult> ExchangePublicTokenAsync(string publicToken, CancellationToken cancellationToken = default);

    /// <summary>Fetch institution + item metadata for an existing access_token.</summary>
    Task<PlaidItemMetadata> GetItemMetadataAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>List Plaid accounts associated with the access_token.</summary>
    Task<IReadOnlyList<PlaidAccountDto>> GetAccountsAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>Run a complete /transactions/sync paginated walk, returning all deltas since <paramref name="cursor"/>.</summary>
    Task<PlaidSyncResult> SyncTransactionsAsync(string accessToken, string? cursor, CancellationToken cancellationToken = default);

    /// <summary>Revoke a Plaid item by access_token. Idempotent — safe to call if already removed.</summary>
    Task RemoveItemAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Register/update the webhook URL on an existing item via <c>/item/webhook/update</c>.
    /// Used to attach the webhook to links created before <see cref="PlaidOptions.WebhookUrl"/> was configured.
    /// </summary>
    Task UpdateWebhookAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch the JSON Web Key (by <paramref name="keyId"/>) used to verify a webhook's <c>Plaid-Verification</c>
    /// JWT signature via <c>/webhook_verification_key/get</c>. Returns the raw JWK — no verification here.
    /// </summary>
    Task<PlaidJwkDto> GetWebhookVerificationKeyAsync(string keyId, CancellationToken cancellationToken = default);
}
