// [SPIKE] BUD-4 — throwaway. Do not merge to main without replacing SpikePlaidAccessor.

namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>
/// Encapsulates all communication with the Plaid API.
/// </summary>
public interface IPlaidAccessor
{
    /// <summary>
    /// Requests a short-lived Link token from Plaid for the given user.
    /// </summary>
    /// <param name="userId">Internal user identifier used as the Plaid client_user_id.</param>
    /// <returns>The link_token string returned by Plaid.</returns>
    Task<string> CreateLinkTokenAsync(int userId);

    /// <summary>
    /// Exchanges a public_token (obtained from Plaid Link) for a permanent access_token.
    /// </summary>
    /// <param name="publicToken">The public_token returned by Plaid Link on success.</param>
    /// <returns>The access_token string returned by Plaid.</returns>
    Task<string> ExchangePublicTokenAsync(string publicToken);
}
