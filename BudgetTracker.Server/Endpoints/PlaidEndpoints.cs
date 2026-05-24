// [SPIKE] BUD-4 — throwaway. Do not merge to main without replacing SpikePlaidAccessor.

using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;

namespace BudgetTracker.Server.Endpoints;

/// <summary>
/// Plaid Link OAuth endpoints. Spike-only — no persistence, no schema changes.
/// </summary>
public static class PlaidEndpoints
{
    /// <summary>
    /// Registers the two Plaid endpoints on the provided route group.
    /// </summary>
    /// <param name="group">The route group to attach endpoints to.</param>
    /// <returns>The same route group for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapPlaidEndpoints(this IEndpointRouteBuilder group)
    {
        // POST /api/plaid/link-token
        // Returns a short-lived Plaid Link token for the current user.
        group.MapPost("/link-token", async (
            IPlaidManager manager,
            ICurrentUserProvider currentUser) =>
        {
            var result = await manager.CreateLinkTokenAsync(currentUser.UserId);
            return result.IsSuccess
                ? Results.Ok(new { linkToken = result.Value })
                : Results.Problem(result.Error, statusCode: 502);
        });

        // POST /api/plaid/exchange-token
        // Exchanges a Plaid public_token for an access_token.
        // The access_token is logged at Warning with [SPIKE] prefix and is never returned to the client.
        group.MapPost("/exchange-token", async (
            ExchangeTokenRequest request,
            IPlaidManager manager) =>
        {
            var result = await manager.ExchangePublicTokenAsync(request.PublicToken);
            return result.IsSuccess
                ? Results.Ok()
                : Results.BadRequest(result.Error);
        });

        return group;
    }
}

/// <summary>Request body for the exchange-token endpoint.</summary>
/// <param name="PublicToken">The public_token from Plaid Link's onSuccess callback.</param>
internal record ExchangeTokenRequest(string PublicToken);
