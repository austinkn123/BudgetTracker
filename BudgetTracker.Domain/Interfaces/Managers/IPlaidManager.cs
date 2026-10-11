using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>
/// Read-only view of a user's current Plaid connection, safe to return over the API.
/// Excludes any reference to the encrypted access_token.
/// </summary>
public record PlaidConnectionView(
    int PlaidItemId,
    string InstitutionName,
    DateTime? LastSyncedAt,
    IReadOnlyList<PlaidLinkedAccountView> Accounts,
    bool NeedsReconnect);

/// <summary>One linked Plaid account, exposed to the client for display.</summary>
public record PlaidLinkedAccountView(string PlaidAccountId, string Name, string? Mask, string AccountType);

/// <summary>Outcome of a sync operation.</summary>
public record PlaidSyncSummary(int Inserted, int Updated, int Removed, DateTime SyncedAt);

/// <summary>
/// Orchestrates Plaid Link, token exchange, transaction sync, and connection management.
/// Owns transaction boundaries (e.g. the atomic replace-connection flow).
/// </summary>
public interface IPlaidManager
{
    /// <summary>Fetch a Link token for the current user, on demand (called when the user clicks "Connect").</summary>
    Task<Result<PlaidLinkTokenResult>> CreateLinkTokenAsync(int userId, string cognitoSub);

    /// <summary>
    /// Fetch an update-mode Link token for one of the user's active connections, so the user can sign in to the bank
    /// again without relinking. Fails with "Bank connection not found" when the item does not exist, belongs to another
    /// user, or is inactive.
    /// </summary>
    Task<Result<PlaidLinkTokenResult>> CreateUpdateLinkTokenAsync(int userId, string cognitoSub, int plaidItemId);

    /// <summary>
    /// Exchange a Plaid Link public_token for an access_token, persist the new PlaidItem alongside any existing ones,
    /// and run the initial transaction sync (~30 days). Returns a sync summary.
    /// </summary>
    Task<Result<PlaidSyncSummary>> ExchangePublicTokenAsync(int userId, string publicToken);

    /// <summary>
    /// Re-sync transactions for the user's active PlaidItems and return the counts aggregated across them.
    /// Idempotent via Plaid.transaction_id dedupe. A user with nothing linked (or nothing due) gets a zero summary.
    /// </summary>
    /// <param name="userId">The current user.</param>
    /// <param name="staleAfterHours">
    /// When set, only items never synced or last synced more than this many hours ago are refreshed; null syncs all.
    /// Negative values are rejected.
    /// </param>
    Task<Result<PlaidSyncSummary>> SyncAsync(int userId, int? staleAfterHours = null);

    /// <summary>
    /// Handle an inbound Plaid webhook. Verifies the <c>Plaid-Verification</c> JWT (ES256 + freshness + body hash);
    /// on any failure returns a silent no-op success so the endpoint can answer a neutral 200 that leaks nothing.
    /// A verified, actionable <c>TRANSACTIONS</c> webhook runs the existing sync for the identified item; an
    /// <c>ITEM</c> webhook reporting a broken or repaired login sets or clears its reconnect flag.
    /// </summary>
    /// <param name="plaidVerificationJwt">The raw JWT from the <c>Plaid-Verification</c> request header.</param>
    /// <param name="rawBody">The verbatim webhook body (needed to verify the body-hash claim and to parse the event).</param>
    Task<Result> HandleWebhookAsync(string plaidVerificationJwt, string rawBody);

    /// <summary>
    /// Backup sweep: re-sync every active PlaidItem so stale data never persists. Per-item failures are
    /// logged and skipped so one bad item never aborts the run. Also opportunistically registers the
    /// configured webhook on pre-existing items.
    /// </summary>
    Task<Result> SweepAllAsync();

    /// <summary>Return one view per active connection for the user; empty when nothing is linked.</summary>
    Task<IReadOnlyList<PlaidConnectionView>> GetConnectionsAsync(int userId);

    /// <summary>
    /// Revoke one Plaid item server-side (best-effort) and soft-delete it. Fails with "Bank connection not found"
    /// when the item does not exist, belongs to another user, or is already inactive.
    /// </summary>
    Task<Result> DisconnectAsync(int userId, int plaidItemId);

    /// <summary>
    /// Clear the reconnect flag on one of the user's active connections after update-mode Link succeeds. A successful
    /// sync does not clear it, because <c>PENDING_EXPIRATION</c>/<c>PENDING_DISCONNECT</c> flag a login that still works.
    /// Fails with "Bank connection not found" when the item does not exist, belongs to another user, or is inactive.
    /// </summary>
    Task<Result> MarkReconnectedAsync(int userId, int plaidItemId);
}
