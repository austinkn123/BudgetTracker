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

        plaidGroup.MapPost("/sync", async (IPlaidManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.SyncAsync(currentUser.UserId);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        });

        plaidGroup.MapGet("/connection", async (IPlaidManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.GetConnectionAsync(currentUser.UserId);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });

        plaidGroup.MapDelete("/connection", async (IPlaidManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.DisconnectAsync(currentUser.UserId);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.BadRequest(new { error = result.Error });
        });

        // Plaid sends no auth token, so this route opts out of the group's RequireAuthorization().
        // Logic lives in HandleWebhookAsync so the raw-body/header/always-200 contract is unit-testable.
        plaidGroup.MapPost("/webhook", HandleWebhookAsync)
            .AllowAnonymous();

        return plaidGroup;
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
