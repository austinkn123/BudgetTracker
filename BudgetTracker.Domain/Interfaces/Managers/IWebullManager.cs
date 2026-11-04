using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>
/// Orchestrates the read-only Webull balance. Touches no budget data: no Transactions, Accounts, Budget Plans or Cash Flow.
/// <see cref="StartVerificationAsync"/> is the only path that creates a token, so no SMS is ever sent without the user
/// clicking "Verify in Webull app". When Webull is not configured, no method makes a Webull call.
/// </summary>
public interface IWebullManager
{
    /// <summary>The stored view. Makes no Webull call.</summary>
    Task<WebullConnectionView> GetAsync(int userId);

    /// <summary>
    /// Advance whatever can advance without creating a token, then return the view. Follows
    /// <see cref="Engines.IWebullEngine.DecideRefresh"/>: checks a pending token (and, if it is now approved, fetches
    /// balances in the same call), or fetches balances when they are due. A Webull failure is stored as the last error and
    /// shows on the returned view; it is never thrown, and the call is not retried.
    /// From Phase 2, "fetch balances" means <see cref="Accessors.IWebullAccessor.GetPortfolioAsync"/> followed by
    /// <see cref="Accessors.IWebullConnectionAccessor.SavePortfolioAsync"/>, dated with
    /// <see cref="Engines.IWebullEngine.ToEasternDate"/>. <see cref="RunCloseSnapshotsAsync"/> uses the same path.
    /// </summary>
    Task<WebullConnectionView> RefreshAsync(int userId);

    /// <summary>
    /// The Investments page. Reads only what is saved, with no Webull call: <see cref="GetAsync"/>'s view plus the saved
    /// accounts, valued by <see cref="Engines.IWebullHoldingsEngine.BuildView"/>. When Webull is not configured, it
    /// returns NotSetUp with no accounts and reads nothing.
    /// </summary>
    Task<WebullHoldingsView> GetHoldingsAsync(int userId);

    /// <summary>
    /// The scheduled market-close snapshot, for every user with a stored token. For each user,
    /// <see cref="Engines.IWebullEngine.IsCloseRunDue"/> decides. When the run is due, it fetches and saves along the same
    /// path as <see cref="RefreshAsync"/>; otherwise it makes no Webull call. It never creates or checks a token, so it can
    /// never send an SMS. A Webull failure is stored as that user's last error, exactly like an on-demand failure. Fetch
    /// time only advances on success, so a failed run never delays the next on-demand refresh. Does nothing when Webull is
    /// not configured. Locally, a hosted service calls this on a timer; on Lambda, an EventBridge Scheduler invocation does.
    /// </summary>
    Task RunCloseSnapshotsAsync();

    /// <summary>
    /// The user's "Verify in Webull app" click: create a token (an SMS in production), store it, then run
    /// <see cref="RefreshAsync"/>, so a sandbox token, which is approved at creation, reaches Connected and fetches at once.
    /// Fails, with no Webull call, when the current view's <see cref="WebullConnectionView.CanVerify"/> is false
    /// (not set up, waiting for approval, or connected). A Webull failure is stored and shows on the returned view; it is not a failure result.
    /// </summary>
    Task<Result<WebullConnectionView>> StartVerificationAsync(int userId);
}
