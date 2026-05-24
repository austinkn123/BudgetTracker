// [SPIKE] BUD-4 — throwaway. Do not merge to main without replacing SpikePlaidAccessor.

using BudgetTracker.Domain.Common;

namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>
/// Orchestrates the Plaid Link OAuth round-trip flow.
/// </summary>
public interface IPlaidManager
{
    /// <summary>
    /// Creates a Plaid Link token for the given user.
    /// </summary>
    /// <param name="userId">Internal user identifier.</param>
    /// <returns>A <see cref="Result{T}"/> containing the link_token on success.</returns>
    Task<Result<string>> CreateLinkTokenAsync(int userId);

    /// <summary>
    /// Exchanges a Plaid public_token for an access_token and logs it (spike only — never persisted or returned).
    /// </summary>
    /// <param name="publicToken">The public_token from Plaid Link's onSuccess callback.</param>
    /// <returns>A <see cref="Result"/> indicating success or failure.</returns>
    Task<Result> ExchangePublicTokenAsync(string publicToken);
}
