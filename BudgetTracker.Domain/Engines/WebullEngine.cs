using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Engines;

/// <summary>
/// Pure Webull rules: the 5-minute approval window, the 15-minute fetch threshold, the 24-hour staleness warning, and
/// the mapping from stored token status and last error to the single <see cref="WebullState"/> the UI shows. No I/O.
/// </summary>
public class WebullEngine : IWebullEngine
{
    private static readonly TimeSpan ApprovalWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FetchInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);
    private static readonly TimeSpan CloseTime = new(16, 30, 0);

    /// <inheritdoc />
    public WebullRefreshAction DecideRefresh(WebullSnapshot? snapshot, DateTime now)
    {
        if (snapshot?.TokenStatus is not { } status || snapshot.LastError == WebullErrorKind.TokenRejected)
            return WebullRefreshAction.None;

        return status switch
        {
            WebullTokenStatus.Pending => WebullRefreshAction.CheckToken,
            WebullTokenStatus.Normal when snapshot.LastFetchedAt is not { } fetchedAt
                                          || fetchedAt < snapshot.TokenCreatedAt
                                          || now - fetchedAt >= FetchInterval => WebullRefreshAction.FetchBalances,
            _ => WebullRefreshAction.None
        };
    }

    /// <inheritdoc />
    public WebullConnectionView BuildView(bool isConfigured, WebullSnapshot? snapshot, DateTime now)
    {
        if (!isConfigured)
            return new WebullConnectionView(WebullState.NotSetUp, [], null, false, null, null, false);

        var approvalExpiresAt = snapshot?.TokenCreatedAt + ApprovalWindow;
        var state = snapshot switch
        {
            null or { TokenStatus: null } => WebullState.NotConnected,
            { LastError: WebullErrorKind.TokenRejected } or { TokenStatus: WebullTokenStatus.Invalid } => WebullState.NeedsVerification,
            { TokenStatus: WebullTokenStatus.Pending } when now < approvalExpiresAt => WebullState.WaitingForApproval,
            { TokenStatus: WebullTokenStatus.Pending or WebullTokenStatus.Expired } => WebullState.ApprovalExpired,
            _ => WebullState.Connected
        };

        return new WebullConnectionView(
            state,
            snapshot?.Balances ?? [],
            snapshot?.LastFetchedAt,
            now - snapshot?.LastFetchedAt > StaleAfter,
            snapshot?.LastError,
            state == WebullState.WaitingForApproval ? approvalExpiresAt : null,
            state is WebullState.NotConnected or WebullState.ApprovalExpired or WebullState.NeedsVerification);
    }

    /// <inheritdoc />
    public DateOnly ToEasternDate(DateTime utc) => EasternTime.ToEasternDate(utc);

    /// <inheritdoc />
    public bool IsCloseRunDue(WebullSnapshot? snapshot, DateTime now)
    {
        if (snapshot is not { TokenStatus: WebullTokenStatus.Normal } || snapshot.LastError == WebullErrorKind.TokenRejected)
            return false;

        return snapshot.LastFetchedAt is not { } fetchedAt || fetchedAt < LatestCloseUtc(now);
    }

    /// <summary>The latest 4:30 pm Eastern Monday-to-Friday close at or before <paramref name="nowUtc"/>, in UTC.</summary>
    private static DateTime LatestCloseUtc(DateTime nowUtc)
    {
        var eastern = EasternTime.ToEastern(nowUtc);
        var day = eastern.TimeOfDay >= CloseTime ? eastern.Date : eastern.Date.AddDays(-1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(-1);

        // 4:30 pm is never inside a DST gap or overlap (those happen at 2 am), so this conversion is unambiguous.
        return TimeZoneInfo.ConvertTimeToUtc(day + CloseTime, EasternTime.Zone);
    }
}
