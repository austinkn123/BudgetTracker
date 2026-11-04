using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>
/// Pure Webull rules. No I/O. Owns the 5-minute approval window, the 15-minute refresh threshold, the 24-hour staleness
/// warning, the mapping from stored token status and last error to the single <see cref="WebullState"/> the UI shows,
/// and the Eastern-time clock: the date daily records are keyed by and the weekday 4:30 pm close run.
/// </summary>
public interface IWebullEngine
{
    /// <summary>
    /// Decide what a refresh should do. Never decides to create a token.
    /// <list type="bullet">
    /// <item><see cref="WebullRefreshAction.None"/> when nothing is stored, no token exists, the token is Expired or Invalid,
    /// or the last error is <see cref="WebullErrorKind.TokenRejected"/>.</item>
    /// <item><see cref="WebullRefreshAction.CheckToken"/> when the token is Pending, at any age (the user may have approved it
    /// while Settings was closed).</item>
    /// <item><see cref="WebullRefreshAction.FetchBalances"/> when the token is Normal and there has been no fetch, the last
    /// fetch is older than the token, or the last fetch is 15 minutes old or more.</item>
    /// <item><see cref="WebullRefreshAction.None"/> otherwise (a Normal token fetched under 15 minutes ago).</item>
    /// </list>
    /// </summary>
    /// <param name="snapshot">What is stored, or null when nothing is.</param>
    /// <param name="now">Current time (UTC), supplied by the caller so the rule stays deterministic.</param>
    WebullRefreshAction DecideRefresh(WebullSnapshot? snapshot, DateTime now);

    /// <summary>
    /// Build the view both cards render. State, first match wins:
    /// <list type="number">
    /// <item>not configured: <see cref="WebullState.NotSetUp"/>, with no balances;</item>
    /// <item>no token: <see cref="WebullState.NotConnected"/>;</item>
    /// <item>last error <see cref="WebullErrorKind.TokenRejected"/>, or token Invalid: <see cref="WebullState.NeedsVerification"/>;</item>
    /// <item>token Pending and <paramref name="now"/> before creation + 5 minutes: <see cref="WebullState.WaitingForApproval"/>;</item>
    /// <item>token Pending past that, or Expired: <see cref="WebullState.ApprovalExpired"/>;</item>
    /// <item>token Normal: <see cref="WebullState.Connected"/>.</item>
    /// </list>
    /// <see cref="WebullConnectionView.IsStale"/> is true when the last fetch is more than 24 hours before
    /// <paramref name="now"/>. <see cref="WebullConnectionView.ApprovalExpiresAt"/> is set only while waiting.
    /// <see cref="WebullConnectionView.CanVerify"/> is true only in NotConnected, ApprovalExpired and NeedsVerification.
    /// </summary>
    /// <param name="isConfigured">Whether both Webull keys are configured.</param>
    /// <param name="snapshot">What is stored, or null when nothing is.</param>
    /// <param name="now">Current time (UTC).</param>
    WebullConnectionView BuildView(bool isConfigured, WebullSnapshot? snapshot, DateTime now);

    /// <summary>
    /// The US-Eastern calendar date of <paramref name="utc"/>. Daily value records are keyed by this date. Delegates to
    /// <see cref="EasternTime.ToEasternDate"/>, the only place that converts to Eastern time, so DST is handled once: the
    /// second Sunday of March, the first Sunday of November, at 2:00 local.
    /// </summary>
    /// <param name="utc">A UTC instant. A value read back from a <c>timestamp without time zone</c> column must have its
    /// kind set to UTC first.</param>
    DateOnly ToEasternDate(DateTime utc);

    /// <summary>
    /// Whether the scheduled market-close run should fetch now. True only when every condition holds:
    /// <list type="bullet">
    /// <item>the token is Normal and the last error is not <see cref="WebullErrorKind.TokenRejected"/>. Any other token
    /// state means no Webull call: the run never checks or creates a token;</item>
    /// <item>a close has passed with no fetch since it. A close is 4:30 pm Eastern on a Monday-to-Friday Eastern date.
    /// Find the latest close at or before <paramref name="now"/>; the run is due when there has been no fetch, or the last
    /// fetch was before that close.</item>
    /// </list>
    /// The rule is idempotent, so a scheduler retry, a late timer tick, or a catch-up after the machine was off all
    /// reduce to "fetch once after the latest close". Market holidays are not excluded.
    /// </summary>
    /// <param name="snapshot">What is stored, or null when nothing is.</param>
    /// <param name="now">Current time (UTC).</param>
    bool IsCloseRunDue(WebullSnapshot? snapshot, DateTime now);
}
