// [SPIKE] BUD-4 — throwaway. Do not merge to main without replacing SpikePlaidAccessor.

using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Managers;

namespace BudgetTracker.Server.Managers;

/// <summary>
/// Orchestrates the Plaid Link OAuth round-trip: link-token creation and public-token exchange.
/// No business logic lives here — delegation only.
/// </summary>
public class PlaidManager(IPlaidAccessor accessor, ILogger<PlaidManager> logger) : IPlaidManager
{
    /// <inheritdoc />
    public async Task<Result<string>> CreateLinkTokenAsync(int userId)
    {
        try
        {
            var linkToken = await accessor.CreateLinkTokenAsync(userId);
            return Result<string>.Success(linkToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Plaid link token for userId={UserId}", userId);
            return Result<string>.Failure(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result> ExchangePublicTokenAsync(string publicToken)
    {
        if (string.IsNullOrEmpty(publicToken))
            return Result.Failure("public_token is required");

        try
        {
            var accessToken = await accessor.ExchangePublicTokenAsync(publicToken);

            // [SPIKE] access_token is intentionally never returned to the client.
            // Logged at Warning so it is visible in dev logs while remaining out of API responses.
            logger.LogWarning("[SPIKE] Plaid access_token received (do not ship to production): {AccessToken}", accessToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to exchange Plaid public token");
            return Result.Failure(ex.Message);
        }
    }
}
