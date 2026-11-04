using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Plaid;
using BudgetTracker.Domain.Webull;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Server.Managers;

/// <inheritdoc />
public class WebullManager(
    IWebullEngine engine,
    IWebullHoldingsEngine holdingsEngine,
    IWebullAccessor webull,
    IWebullConnectionAccessor connections,
    IOptions<WebullOptions> options,
    ILogger<WebullManager> logger) : IWebullManager
{
    /// <inheritdoc />
    public async Task<WebullConnectionView> GetAsync(int userId)
    {
        var now = DateTime.UtcNow;
        return options.Value.IsConfigured
            ? engine.BuildView(true, await connections.GetAsync(userId), now)
            : engine.BuildView(false, null, now);
    }

    /// <inheritdoc />
    public async Task<WebullConnectionView> RefreshAsync(int userId)
    {
        var now = DateTime.UtcNow;
        if (!options.Value.IsConfigured)
            return engine.BuildView(false, null, now);

        var action = engine.DecideRefresh(await connections.GetAsync(userId), now);
        if (action != WebullRefreshAction.None)
            await AdvanceAsync(userId, action, now);

        return engine.BuildView(true, await connections.GetAsync(userId), now);
    }

    /// <inheritdoc />
    public async Task<Result<WebullConnectionView>> StartVerificationAsync(int userId)
    {
        var view = await GetAsync(userId);
        if (!view.CanVerify)
            return Result<WebullConnectionView>.Failure($"Webull verification is not available while {view.State}.");

        try
        {
            var token = await webull.CreateTokenAsync();
            await connections.SaveTokenAsync(userId, token.AccessToken, token.Status, DateTime.UtcNow);
        }
        catch (WebullApiException ex)
        {
            // The message is sanitized (HTTP status and error_code only); error_code tells a bad key from a bad signature.
            logger.LogWarning(ex, "Webull {Operation} failed ({Kind}).", "create", ex.Kind);
            await connections.SetLastErrorAsync(userId, ex.Kind);
            return Result<WebullConnectionView>.Success(await GetAsync(userId));
        }

        // A sandbox token is approved at creation, so this reaches Connected and fetches at once.
        return Result<WebullConnectionView>.Success(await RefreshAsync(userId));
    }

    /// <inheritdoc />
    public async Task<WebullHoldingsView> GetHoldingsAsync(int userId)
    {
        var view = await GetAsync(userId);
        return holdingsEngine.BuildView(view, options.Value.IsConfigured ? await connections.GetSavedPortfolioAsync(userId) : []);
    }

    /// <inheritdoc />
    public async Task RunCloseSnapshotsAsync()
    {
        if (!options.Value.IsConfigured)
            return;

        foreach (var userId in await connections.GetUserIdsWithTokenAsync())
        {
            try
            {
                var now = DateTime.UtcNow;
                if (!engine.IsCloseRunDue(await connections.GetAsync(userId), now))
                    continue;

                // Never checks or creates a token: no SMS without the user's click.
                if (await ReadTokenAsync(userId) is { } accessToken)
                    await FetchPortfolioAsync(userId, accessToken, now);
            }
            catch (Exception ex)
            {
                // One user's failure (e.g. a daily-row key race with a manual refresh) must not stop the others.
                logger.LogWarning(ex, "Webull close snapshot failed for user {UserId}.", userId);
            }
        }
    }

    /// <summary>
    /// Runs one refresh step: check a pending token (then fetch only if that approved it, never a second check), or fetch
    /// the portfolio. A missing or unreadable token, or any Webull failure, is stored as the last error. No retries.
    /// </summary>
    private async Task AdvanceAsync(int userId, WebullRefreshAction action, DateTime now)
    {
        if (await ReadTokenAsync(userId) is not { } accessToken)
            return;

        if (action == WebullRefreshAction.CheckToken)
        {
            try
            {
                await connections.SetTokenStatusAsync(userId, await webull.CheckTokenAsync(accessToken));
            }
            catch (WebullApiException ex)
            {
                await RecordFailureAsync(userId, "check", ex);
                return;
            }

            if (engine.DecideRefresh(await connections.GetAsync(userId), now) != WebullRefreshAction.FetchBalances)
                return;
        }

        await FetchPortfolioAsync(userId, accessToken, now);
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

    /// <summary>Fetches balances and positions and saves them, dated in Eastern time. A Webull failure is stored, not thrown.</summary>
    private async Task FetchPortfolioAsync(int userId, string accessToken, DateTime now)
    {
        try
        {
            var portfolio = await webull.GetPortfolioAsync(accessToken);
            await connections.SavePortfolioAsync(userId, portfolio, now, engine.ToEasternDate(now));
        }
        catch (WebullApiException ex)
        {
            await RecordFailureAsync(userId, "portfolio", ex);
        }
    }

    private async Task RecordFailureAsync(int userId, string operation, WebullApiException ex)
    {
        // The message is sanitized (HTTP status and error_code only).
        logger.LogWarning(ex, "Webull {Operation} failed ({Kind}).", operation, ex.Kind);
        await connections.SetLastErrorAsync(userId, ex.Kind);
    }
}
