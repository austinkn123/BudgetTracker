using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>
/// Pure realized-P/L computation, average-cost method, before fees. No I/O. The only place that knows which Webull order
/// statuses, sides and instrument types count. Computed on every read from the raw orders: a late-arriving older order
/// changes later averages, so nothing derived is stored.
/// </summary>
public interface IWebullTradesEngine
{
    /// <summary>
    /// Build the Trades view.
    /// <list type="bullet">
    /// <item>Counted orders: status FILLED or PARTIAL_FILLED with FilledQuantity above 0. Every other order is ignored, never
    /// counted as excluded.</item>
    /// <item>Valued orders: counted, InstrumentType EQUITY or CRYPTO, side BUY or SELL. Any other counted order (OPTION,
    /// FUTURES, EVENT, missing type, or side SHORT) adds 1 to ExcludedUnsupported when its close date is in the range.</item>
    /// <item>Fill time = FilledAt, else PlacedAt. Close date = <see cref="EasternTime.ToEasternDate"/> of fill time.</item>
    /// <item>One running position per (AccountId, Symbol), replayed over all history in order of fill time, then PlacedAt,
    /// then BUY before SELL, then OrderId (ordinal).</item>
    /// <item>BUY: quantity += q, total cost += q × price. A missing or non-positive price marks the position's cost unknown.</item>
    /// <item>SELL of q with tolerance t = 0.00001 × fills since the position was last empty (Webull reports quantities to 5
    /// decimals): when q &gt; quantity + t, the sale is excluded and the position resets to empty. When the cost is unknown or
    /// the price is missing or non-positive, the sale is excluded and reduces the position like any sale. Otherwise it closes
    /// a trade at average cost = total cost ÷ quantity; quantity −= q and total cost −= average × q; the average is unchanged.
    /// An excluded sale adds 1 to ExcludedIncompleteCost when its close date is in the range.</item>
    /// <item>When quantity falls to t or below, the position resets: quantity 0, total cost 0, cost known, fill count 0.</item>
    /// <item>The range is [<see cref="EasternTime.RangeStart"/>(range, today), today] in Eastern dates, today being
    /// <see cref="EasternTime.ToEasternDate"/>(<paramref name="now"/>). Cost history before the range still counts.</item>
    /// <item>Trades are sorted by fill time, newest first, then OrderId. Summary rules are on <see cref="WebullTradeSummary"/>.</item>
    /// <item>When <paramref name="history"/>'s state is NotSetUp, no trades and a zero summary.</item>
    /// </list>
    /// </summary>
    /// <param name="history">From <see cref="IWebullOrderSyncEngine.BuildStatus"/>; copied onto the view.</param>
    /// <param name="orders">Every saved order, all accounts and statuses.</param>
    /// <param name="range">The selected range.</param>
    /// <param name="now">Current time (UTC).</param>
    WebullTradesView BuildView(WebullTradeHistoryStatus history, IReadOnlyList<WebullOrder> orders, WebullRange range, DateTime now);
}
