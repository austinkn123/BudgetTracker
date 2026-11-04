using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>
/// Pure order-history sync planning. No I/O. Decides which window of Webull's order history to fetch next and how a page
/// moves an account's <see cref="WebullOrderSync"/>. Separate from <see cref="IWebullTradesEngine"/>: paging and Webull's
/// history limits change for different reasons than the cost-basis rules.
/// History is walked backward in windows that never cross a UTC calendar year, down to Webull's floor, 2018-05-21T00:00Z.
/// A window is paged to its end with Webull's cursor before coverage moves; the cursor is persisted, so a window bigger than
/// one sync resumes next time.
/// </summary>
public interface IWebullOrderSyncEngine
{
    /// <summary>
    /// The window to fetch next, or null when there is nothing to do. First match wins:
    /// <list type="number">
    /// <item>a window is in progress: that window, with its key;</item>
    /// <item>nothing covered yet: the top window, [max(floor, January 1 of <paramref name="now"/>'s UTC year), <paramref name="now"/>];</item>
    /// <item>CoveredTo is 15 minutes or more before <paramref name="now"/>: the incremental window, ending <paramref name="now"/>,
    /// starting at the earlier of (the newest saved order's PlacedAt, or CoveredTo when there are none) − 7 days, and the oldest
    /// saved order still PENDING, SUBMITTED or PARTIAL_FILLED, ignoring any placed more than 90 days before
    /// <paramref name="now"/>; never before the floor. It may cross a year;</item>
    /// <item>CoveredFrom is after the floor: the backfill window ending 1 ms before CoveredFrom and starting at the later of the
    /// floor and January 1 of that end's UTC year;</item>
    /// <item>otherwise null.</item>
    /// </list>
    /// </summary>
    /// <param name="sync">The account's progress; a new account has every field null.</param>
    /// <param name="accountOrders">That account's saved orders.</param>
    /// <param name="now">The sync's start time (UTC). Pass the same value for the whole sync.</param>
    WebullOrderWindow? NextWindow(WebullOrderSync sync, IReadOnlyList<WebullOrder> accountOrders, DateTime now);

    /// <summary>
    /// Progress after a page of <paramref name="window"/> was fetched. Clears LastError. With a
    /// <paramref name="nextPaginationKey"/>, the window stays in progress with that key and coverage is unchanged. Without
    /// one, the window is complete: no window in progress, CoveredFrom = min(CoveredFrom, window start), CoveredTo =
    /// max(CoveredTo, window end). Windows from <see cref="NextWindow"/> always touch the covered span, so it stays gap-free.
    /// </summary>
    WebullOrderSync Advance(WebullOrderSync sync, WebullOrderWindow window, string? nextPaginationKey);

    /// <summary>
    /// Progress after a failed fetch: records <paramref name="error"/>, keeps coverage and the window in progress, and drops
    /// the window's key, so the next sync restarts that window from its first page (saved orders are overwritten, never doubled).
    /// </summary>
    WebullOrderSync Fail(WebullOrderSync sync, WebullErrorKind error);

    /// <summary>
    /// Progress across the user's current accounts (the balance snapshot's AccountIds). Syncs for other accounts are ignored;
    /// an account with no sync counts as nothing covered. See <see cref="WebullTradeHistoryStatus"/> for each field.
    /// </summary>
    WebullTradeHistoryStatus BuildStatus(WebullState state, IReadOnlyList<string> accountIds, IReadOnlyList<WebullOrderSync> syncs);
}
