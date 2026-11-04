using System.Net;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Plaid;
using BudgetTracker.Domain.Webull;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Server.Managers;

/// <inheritdoc />
/// <param name="clock">Wall time, elapsed time for the time box, and the 429 backoff; defaults to <see cref="TimeProvider.System"/>.</param>
public class WebullTradesManager(
    IWebullEngine engine,
    IWebullOrderSyncEngine syncEngine,
    IWebullTradesEngine tradesEngine,
    IWebullOrderHistoryAccessor history,
    IWebullConnectionAccessor connections,
    IWebullOrderAccessor orders,
    IOptions<WebullOptions> options,
    ILogger<WebullTradesManager> logger,
    TimeProvider? clock = null) : IWebullTradesManager
{
    private const int MaxPagesPerCall = 12;
    private static readonly TimeSpan TimeBox = TimeSpan.FromSeconds(15);
    private const int MaxThrottledInARow = 3;
    private static readonly TimeSpan ThrottleBackoff = TimeSpan.FromSeconds(2.5);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <inheritdoc />
    public async Task<WebullTradeHistoryStatus> SyncAsync(int userId)
    {
        var started = _clock.GetTimestamp();
        var now = _clock.GetUtcNow().UtcDateTime;
        if (!options.Value.IsConfigured)
            return NotSetUp(now);

        var snapshot = await connections.GetAsync(userId);
        if (engine.BuildView(true, snapshot, now).State == WebullState.Connected)
            await FetchPagesAsync(userId, AccountIds(snapshot), now, started);

        return await StatusAsync(userId, now);
    }

    /// <inheritdoc />
    public async Task<WebullTradesView> GetTradesAsync(int userId, WebullRange range)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        return options.Value.IsConfigured
            ? tradesEngine.BuildView(await StatusAsync(userId, now), await orders.GetOrdersAsync(userId), range, now)
            : tradesEngine.BuildView(NotSetUp(now), [], range, now);
    }

    /// <summary>
    /// Plan → fetch one page → advance → save, account by account, until the engine has nothing left, the time box is
    /// spent, or Webull fails. A 429 waits and retries the same page (the unchanged sync plans the same window and key);
    /// the third in a row is a failure. A failure saves the failed progress (no orders), records TokenRejected on the
    /// connection, and ends the call.
    /// </summary>
    private async Task FetchPagesAsync(int userId, IReadOnlyList<string> accountIds, DateTime now, long started)
    {
        if (await ReadTokenAsync(userId) is not { } accessToken)
            return;

        var syncs = (await orders.GetSyncsAsync(userId)).ToDictionary(s => s.AccountId);
        var savedOrders = await orders.GetOrdersAsync(userId);
        var pages = 0;
        var throttled = 0;
        foreach (var accountId in accountIds)
        {
            var sync = syncs.GetValueOrDefault(accountId) ?? new WebullOrderSync(accountId, null, null, null, null);
            // Saved before this call: only the incremental window reads them, and it is planned at most once per call.
            var accountOrders = savedOrders.Where(o => o.AccountId == accountId).ToList();
            while (syncEngine.NextWindow(sync, accountOrders, now) is { } window)
            {
                if (pages >= MaxPagesPerCall || _clock.GetElapsedTime(started) >= TimeBox)
                    return;

                WebullOrderPage page;
                try
                {
                    page = await history.GetOrderPageAsync(accessToken, accountId, window);
                }
                catch (WebullApiException ex)
                {
                    if (ex.StatusCode == HttpStatusCode.TooManyRequests && ++throttled < MaxThrottledInARow)
                    {
                        logger.LogWarning("Webull {Operation} hit HTTP 429; retry {Attempt} of {MaxRetries} in {Seconds}s.",
                            "order history", throttled, MaxThrottledInARow - 1, ThrottleBackoff.TotalSeconds);
                        await Task.Delay(ThrottleBackoff, _clock);
                        continue; // the time box is checked again before the retry
                    }

                    // The message is sanitized (HTTP status and error_code only).
                    logger.LogWarning(ex, "Webull {Operation} failed ({Kind}).", "order history", ex.Kind);
                    await orders.SavePageAsync(userId, syncEngine.Fail(sync, ex.Kind), []);
                    if (ex.Kind == WebullErrorKind.TokenRejected)
                        await connections.SetLastErrorAsync(userId, WebullErrorKind.TokenRejected);
                    return;
                }

                pages++;
                throttled = 0;
                sync = syncEngine.Advance(sync, window, page.NextPaginationKey);
                await orders.SavePageAsync(userId, sync, page.Orders);
            }
        }
    }

    /// <summary>The stored token, or null after recording TokenRejected when it is missing or unreadable.</summary>
    private async Task<string?> ReadTokenAsync(int userId)
    {
        var lookup = await connections.GetAccessTokenAsync(userId);
        if (lookup.Status == AccessTokenStatus.Found)
            return lookup.AccessToken;

        await connections.SetLastErrorAsync(userId, WebullErrorKind.TokenRejected);
        return null;
    }

    private async Task<WebullTradeHistoryStatus> StatusAsync(int userId, DateTime now)
    {
        var snapshot = await connections.GetAsync(userId);
        return syncEngine.BuildStatus(engine.BuildView(true, snapshot, now).State, AccountIds(snapshot), await orders.GetSyncsAsync(userId));
    }

    private WebullTradeHistoryStatus NotSetUp(DateTime now) => syncEngine.BuildStatus(engine.BuildView(false, null, now).State, [], []);

    private static IReadOnlyList<string> AccountIds(WebullSnapshot? snapshot) =>
        snapshot?.Balances.Select(b => b.AccountId).ToList() ?? [];
}
