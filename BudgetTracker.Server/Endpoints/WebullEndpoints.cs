using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Server.Endpoints;

/// <summary>
/// The read-only Webull balance, holdings and trade history. No request bodies. No route ever returns the App Key, secret
/// or access token.
/// </summary>
public static class WebullEndpoints
{
    /// <summary>
    /// Maps <c>GET /</c>, <c>GET /holdings</c>, <c>GET /trades</c>, <c>POST /refresh</c>, <c>POST /verify</c> and
    /// <c>POST /trades/sync</c> onto <paramref name="webullGroup"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapWebullEndpoints(this IEndpointRouteBuilder webullGroup)
    {
        webullGroup.MapGet("/", GetAsync);
        webullGroup.MapGet("/holdings", GetHoldingsAsync);
        webullGroup.MapPost("/refresh", RefreshAsync);
        webullGroup.MapPost("/verify", VerifyAsync);
        webullGroup.MapGet("/trades", GetTradesAsync);
        webullGroup.MapPost("/trades/sync", SyncTradesAsync);
        return webullGroup;
    }

    /// <summary><c>GET /api/webull</c> — the stored view. 200; makes no Webull call.</summary>
    /// <summary><c>GET /api/webull/holdings</c> — the saved balances and positions, valued. 200; makes no Webull call.</summary>
    public static async Task<IResult> GetHoldingsAsync(IWebullManager manager, ICurrentUserProvider currentUser) =>
        TypedResults.Ok(await manager.GetHoldingsAsync(currentUser.UserId));

    public static async Task<IResult> GetAsync(IWebullManager manager, ICurrentUserProvider currentUser) =>
        TypedResults.Ok(await manager.GetAsync(currentUser.UserId));

    /// <summary>
    /// <c>POST /api/webull/refresh</c> — checks a pending token or fetches balances when due, then returns the view. Always
    /// 200: a Webull failure shows as the view's <c>lastError</c>.
    /// </summary>
    public static async Task<IResult> RefreshAsync(IWebullManager manager, ICurrentUserProvider currentUser) =>
        TypedResults.Ok(await manager.RefreshAsync(currentUser.UserId));

    /// <summary>
    /// <c>POST /api/webull/verify</c> — "Verify in Webull app": creates a token (an SMS in production). 200 with the view;
    /// 409 with <c>{ error }</c> when the current view does not allow verifying.
    /// </summary>
    public static async Task<IResult> VerifyAsync(IWebullManager manager, ICurrentUserProvider currentUser)
    {
        var result = await manager.StartVerificationAsync(currentUser.UserId);
        return result.IsSuccess
            ? TypedResults.Ok(result.Value!)
            : TypedResults.Conflict(new { error = result.Error });
    }

    /// <summary>
    /// <c>GET /api/webull/trades?range=1M|3M|YTD|1Y|All</c> (case-insensitive; YTD when omitted) — the saved trade history's
    /// realized wins and losses for the range. 200; 400 with <c>{ error }</c> for any other range. Makes no Webull call.
    /// The range is mapped here rather than enum-bound, because enum binding would also accept numbers.
    /// </summary>
    public static async Task<IResult> GetTradesAsync(IWebullTradesManager manager, ICurrentUserProvider currentUser, string? range = null)
    {
        WebullRange? parsed = range?.ToUpperInvariant() switch
        {
            null or "" or "YTD" => WebullRange.YearToDate,
            "1M" => WebullRange.OneMonth,
            "3M" => WebullRange.ThreeMonths,
            "1Y" => WebullRange.OneYear,
            "ALL" => WebullRange.All,
            _ => null
        };
        return parsed is { } value
            ? TypedResults.Ok(await manager.GetTradesAsync(currentUser.UserId, value))
            : TypedResults.BadRequest(new { error = "Unknown range. Use 1M, 3M, YTD, 1Y or All." });
    }

    /// <summary>
    /// <c>POST /api/webull/trades/sync</c> — fetches and saves order-history pages within the time box, then returns the
    /// progress. Always 200: a Webull failure shows as the progress's <c>lastError</c>.
    /// </summary>
    public static async Task<IResult> SyncTradesAsync(IWebullTradesManager manager, ICurrentUserProvider currentUser) =>
        TypedResults.Ok(await manager.SyncAsync(currentUser.UserId));
}
