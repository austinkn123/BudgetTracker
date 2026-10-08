using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints;

/// <summary>
/// Minimal-API endpoints for the Plaid integration. All routes require authentication
/// and resolve the current user via <see cref="ICurrentUserProvider"/>.
/// </summary>
public static class PlaidEndpoints
{
    /// <summary>Request body for <c>POST /api/plaid/exchange-token</c>.</summary>
    public record ExchangeTokenRequest(string PublicToken);

    /// <summary>Request body for <c>POST /api/plaid/sandbox/seed</c>. Months defaults to 4.</summary>
    public record SandboxSeedRequest(int? Months);

    public static IEndpointRouteBuilder MapPlaidEndpoints(this IEndpointRouteBuilder plaidGroup)
    {
        plaidGroup.MapPost("/link-token", async (IPlaidManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.CreateLinkTokenAsync(currentUser.UserId, currentUser.CognitoSub);
            return result.IsSuccess
                ? Results.Ok(new { linkToken = result.Value!.LinkToken, expiration = result.Value.Expiration })
                : Results.Problem(detail: result.Error, statusCode: StatusCodes.Status502BadGateway);
        });

        plaidGroup.MapPost("/exchange-token", async (
            [FromBody] ExchangeTokenRequest request,
            IPlaidManager manager,
            ICurrentUserProvider currentUser) =>
        {
            var result = await manager.ExchangePublicTokenAsync(currentUser.UserId, request.PublicToken);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        });

        plaidGroup.MapPost("/sync", SyncAsync);

        // Development-only. Replaces Plaid Sandbox's stock "Tartan Bank" fixtures with transactions
        // derived from the user's own plan, so dashboard figures reconcile. Guarded twice: 404 outside
        // Development, and the manager refuses unless Plaid is configured for sandbox.
        plaidGroup.MapPost("/sandbox/seed", async (
            [FromBody] SandboxSeedRequest? request,
            ISandboxSeedManager manager,
            ICurrentUserProvider currentUser,
            IWebHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            if (!environment.IsDevelopment()) return Results.NotFound();

            var result = await manager.SeedAsync(currentUser.UserId, request?.Months ?? 4, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        });

        plaidGroup.MapGet("/connections", GetConnectionsAsync);

        plaidGroup.MapDelete("/connections/{plaidItemId:int}", DeleteConnectionAsync);

        // Plaid sends no auth token, so this route opts out of the group's RequireAuthorization().
        // Logic lives in HandleWebhookAsync so the raw-body/header/always-200 contract is unit-testable.
        plaidGroup.MapPost("/webhook", HandleWebhookAsync)
            .AllowAnonymous();

        return plaidGroup;
    }

    /// <summary>
    /// <c>POST /api/plaid/sync?staleAfterHours=</c> — syncs the user's active connections and returns the
    /// aggregated <see cref="PlaidSyncSummary"/>; 400 with <c>{ error }</c> on failure.
    /// </summary>
    public static async Task<IResult> SyncAsync(
        [FromQuery] int? staleAfterHours,
        IPlaidManager manager,
        ICurrentUserProvider currentUser)
    {
        var result = await manager.SyncAsync(currentUser.UserId, staleAfterHours);
        return result.IsSuccess
            ? TypedResults.Ok(result.Value!)
            : TypedResults.BadRequest(new { error = result.Error });
    }

    /// <summary><c>GET /api/plaid/connections</c> — every active connection for the user (empty array if none).</summary>
    public static async Task<IResult> GetConnectionsAsync(IPlaidManager manager, ICurrentUserProvider currentUser)
    {
        var connections = await manager.GetConnectionsAsync(currentUser.UserId);
        return TypedResults.Ok(connections);
    }

    /// <summary>
    /// <c>DELETE /api/plaid/connections/{plaidItemId}</c> — revokes and soft-deletes one connection. 204 on success;
    /// 404 when the item is missing, owned by another user, or already inactive.
    /// </summary>
    public static async Task<IResult> DeleteConnectionAsync(
        int plaidItemId,
        IPlaidManager manager,
        ICurrentUserProvider currentUser)
    {
        var result = await manager.DisconnectAsync(currentUser.UserId, plaidItemId);
        return result.IsSuccess ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    /// <summary>
    /// Handles an inbound Plaid webhook: reads the body verbatim (needed for the SHA-256 hash the JWT
    /// signs) and the <c>Plaid-Verification</c> header, delegates verification/sync to the manager, and
    /// ALWAYS returns a neutral 200 so verification success/failure is never revealed to the caller.
    /// </summary>
    public static async Task<IResult> HandleWebhookAsync(HttpRequest request, IPlaidManager manager)
    {
        using var reader = new StreamReader(request.Body);
        var rawBody = await reader.ReadToEndAsync();
        var verificationJwt = request.Headers["Plaid-Verification"].ToString();

        await manager.HandleTransactionsWebhookAsync(verificationJwt, rawBody);
        return Results.Ok();
    }
}
