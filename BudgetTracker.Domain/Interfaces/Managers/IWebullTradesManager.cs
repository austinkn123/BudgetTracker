using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>
/// Orchestrates Webull trade history: syncing orders and reading realized wins and losses. Separate from
/// <see cref="IWebullManager"/> so the balance refresh never waits on a backfill. Never creates or checks a token, so it can
/// never send an SMS. Touches no budget data: no Transactions, Accounts, Budget Plans or Cash Flow. When Webull is not
/// configured, no method makes a Webull call or reads Webull tables.
/// </summary>
public interface IWebullTradesManager
{
    /// <summary>
    /// Fetch and save order-history pages for each current account (the saved balance snapshot's AccountIds, in order), as
    /// far as the time box allows, then return the progress.
    /// <list type="bullet">
    /// <item>Makes no Webull call unless <see cref="Engines.IWebullEngine.BuildView"/> says Connected. A missing or unreadable
    /// token records TokenRejected on the connection, like <see cref="IWebullManager.RefreshAsync"/>.</item>
    /// <item>Per account, loops <see cref="Engines.IWebullOrderSyncEngine.NextWindow"/> → fetch one page →
    /// <see cref="Engines.IWebullOrderSyncEngine.Advance"/> → <see cref="Accessors.IWebullOrderAccessor.SavePageAsync"/>, with one
    /// <c>now</c> for the whole call, until the engine returns null.</item>
    /// <item>Time box: stops before starting a page once 12 pages were fetched or 15 s have passed, so one call stays well
    /// under Lambda's 30 s even when the last page hits the 10 s HTTP timeout. The next call resumes.</item>
    /// <item>An HTTP 429 is not a failure: the call waits 2.5 s and retries the same page (same window and key), within the
    /// time box. If the box runs out, it returns with no error recorded. A page resets the count; the third 429 in a row
    /// is a failure.</item>
    /// <item>A Webull failure saves <see cref="Engines.IWebullOrderSyncEngine.Fail"/>'s progress and ends the call (no retries,
    /// remaining accounts wait). TokenRejected is also recorded on the connection. Never thrown.</item>
    /// </list>
    /// </summary>
    Task<WebullTradeHistoryStatus> SyncAsync(int userId);

    /// <summary>
    /// The Trades view for <paramref name="range"/>. Reads only what is saved, with no Webull call: connection state, sync
    /// progress and every saved order, computed by <see cref="Engines.IWebullTradesEngine.BuildView"/>. When Webull is not
    /// configured, NotSetUp with no trades.
    /// </summary>
    Task<WebullTradesView> GetTradesAsync(int userId, WebullRange range);
}
