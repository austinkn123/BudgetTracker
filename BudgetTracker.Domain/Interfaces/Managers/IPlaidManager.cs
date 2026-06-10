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
    IReadOnlyList<PlaidLinkedAccountView> Accounts);

/// <summary>One linked Plaid account, exposed to the client for display.</summary>
public record PlaidLinkedAccountView(string Name, string? Mask, string AccountType);

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
    /// Exchange a Plaid Link public_token for an access_token, persist the new PlaidItem (replacing any active one),
    /// and run the initial transaction sync (~30 days). Returns a sync summary.
    /// </summary>
    Task<Result<PlaidSyncSummary>> ExchangePublicTokenAsync(int userId, string publicToken);

    /// <summary>Re-sync transactions for the user's active PlaidItem. Idempotent via Plaid.transaction_id dedupe.</summary>
    Task<Result<PlaidSyncSummary>> SyncAsync(int userId);

    /// <summary>Return the user's current active connection metadata, or a NotFound failure if none is linked.</summary>
    Task<Result<PlaidConnectionView>> GetConnectionAsync(int userId);

    /// <summary>Soft-delete the active connection and revoke the Plaid item server-side.</summary>
    Task<Result> DisconnectAsync(int userId);
}
