namespace BudgetTracker.Domain.Webull;

/// <summary>
/// The Investments range buttons. Query values are <c>1M</c>, <c>3M</c>, <c>YTD</c>, <c>1Y</c> and <c>All</c>; the endpoint
/// maps them, because enum binding would also accept numbers. <see cref="EasternTime.RangeStart"/> resolves a range to its
/// first US-Eastern date. The Phase 4 performance chart uses the same ranges.
/// </summary>
public enum WebullRange
{
    OneMonth,
    ThreeMonths,
    YearToDate,
    OneYear,
    All
}

/// <summary>
/// One order exactly as Webull's <c>GET /trading/orders/historical-orders/list</c> reports it, flattened out of its combo
/// group. Stored unchanged, in every status (cancelled and open orders included), so a later status change overwrites the
/// row. What the strings mean (which statuses count, which instrument types are valued) is decided only by
/// <see cref="Interfaces.Engines.IWebullTradesEngine"/>, so a reading fix never needs a data migration.
/// A number Webull sends empty or omits is null, never 0.
/// </summary>
/// <param name="AccountId">The account the order was requested for (Webull's <c>account_id</c> query value).</param>
/// <param name="OrderId">Webull's <c>order_id</c>. Unique within the account.</param>
/// <param name="Symbol">Webull's <c>symbol</c>.</param>
/// <param name="InstrumentType">Webull's <c>instrument_type</c> as sent (<c>EQUITY</c>, <c>CRYPTO</c>, <c>OPTION</c>, …), or null when omitted.</param>
/// <param name="Side">Webull's <c>side</c> as sent (<c>BUY</c>, <c>SELL</c>, <c>SHORT</c>).</param>
/// <param name="Status">Webull's <c>status</c> as sent (<c>FILLED</c>, <c>PARTIAL_FILLED</c>, <c>CANCELLED</c>, …).</param>
/// <param name="FilledQuantity">Webull's <c>filled_quantity</c>. Fractional for crypto and fractional shares; 5 decimals per Webull.</param>
/// <param name="FilledPrice">Webull's <c>filled_price</c>: the average price of the filled quantity.</param>
/// <param name="PlacedAt">Webull's <c>place_time_at</c> (UTC).</param>
/// <param name="FilledAt">Webull's <c>filled_time_at</c>, the last fill (UTC), or null when omitted.</param>
public record WebullOrder(
    string AccountId,
    string OrderId,
    string Symbol,
    string? InstrumentType,
    string Side,
    string Status,
    decimal? FilledQuantity,
    decimal? FilledPrice,
    DateTime PlacedAt,
    DateTime? FilledAt);

/// <summary>One time window of Webull's order history, and where in it the next page starts.</summary>
/// <param name="Start">Sent as <c>start_time</c> (UTC, millisecond precision).</param>
/// <param name="End">Sent as <c>end_time</c> (UTC, millisecond precision).</param>
/// <param name="PaginationKey">Sent as <c>pagination_key</c>; null for the window's first page.</param>
public record WebullOrderWindow(DateTime Start, DateTime End, string? PaginationKey);

/// <summary>One page of Webull's order history.</summary>
/// <param name="Orders">Every order on the page, flattened out of its combo groups, in any status.</param>
/// <param name="NextPaginationKey">Webull's <c>pagination_key</c>; null when Webull omits it, which marks the window's last page.</param>
public record WebullOrderPage(IReadOnlyList<WebullOrder> Orders, string? NextPaginationKey);

/// <summary>
/// How much of one account's order history is saved. Persisted after every page, so a sync resumes where the last one
/// stopped, across restarts. Every order placed in [<see cref="CoveredFrom"/>, <see cref="CoveredTo"/>] has been fetched.
/// </summary>
/// <param name="AccountId">Webull's account id.</param>
/// <param name="CoveredFrom">Oldest instant of the saved, gap-free history (UTC); null until a first window completes.</param>
/// <param name="CoveredTo">Newest instant of it: the end of the last completed window (UTC); null until a first window completes.</param>
/// <param name="Window">The window being paged through, with the next page's key; null when none is in progress.</param>
/// <param name="LastError">Why the last page fetch failed, or null when it succeeded.</param>
public record WebullOrderSync(
    string AccountId,
    DateTime? CoveredFrom,
    DateTime? CoveredTo,
    WebullOrderWindow? Window,
    WebullErrorKind? LastError);

/// <summary>
/// Trade-history progress across the user's current accounts. Returned by <c>POST /api/webull/trades/sync</c> and inside
/// every <see cref="WebullTradesView"/>.
/// </summary>
/// <param name="State">The connection state (<see cref="WebullConnectionView.State"/>). Only Connected syncs.</param>
/// <param name="AsOf">The oldest account's <see cref="WebullOrderSync.CoveredTo"/> (UTC); null when any account has none.</param>
/// <param name="HistoryFrom">US-Eastern date of the newest account's <see cref="WebullOrderSync.CoveredFrom"/>, for
/// "Loading trade history back to &lt;date&gt;"; null when any account has none.</param>
/// <param name="IsComplete">True when every current account's history reaches Webull's 2018-05-21 floor (vacuously true with no accounts).</param>
/// <param name="LastError">The first current account's last sync error, or null.</param>
public record WebullTradeHistoryStatus(
    WebullState State,
    DateTime? AsOf,
    DateOnly? HistoryFrom,
    bool IsComplete,
    WebullErrorKind? LastError);

/// <summary>
/// One SELL fill realized against the average cost at that moment. Money is rounded to cents only where stated;
/// percentages are in percent units rounded to 2 decimals, away from zero.
/// </summary>
/// <param name="AccountId">Webull's account id.</param>
/// <param name="OrderId">Webull's order id. With <paramref name="AccountId"/>, a stable row key.</param>
/// <param name="CloseDate">US-Eastern date of the fill.</param>
/// <param name="Symbol">Webull's symbol.</param>
/// <param name="InstrumentType"><c>EQUITY</c> or <c>CRYPTO</c>.</param>
/// <param name="Quantity">Filled quantity sold.</param>
/// <param name="AverageCost">Average cost per share just before the sale. Not rounded.</param>
/// <param name="SalePrice">Webull's filled price.</param>
/// <param name="RealizedProfitLoss">(SalePrice − AverageCost) × Quantity, rounded to cents. Win/loss is judged on this value.</param>
/// <param name="RealizedProfitLossPercent">(SalePrice − AverageCost) ÷ AverageCost × 100; null when AverageCost is 0.</param>
public record WebullClosedTrade(
    string AccountId,
    string OrderId,
    DateOnly CloseDate,
    string Symbol,
    string InstrumentType,
    decimal Quantity,
    decimal AverageCost,
    decimal SalePrice,
    decimal RealizedProfitLoss,
    decimal? RealizedProfitLossPercent);

/// <summary>The summary band for one range. Before fees, average-cost method, not tax figures.</summary>
/// <param name="TotalRealizedProfitLoss">Sum of the range's <see cref="WebullClosedTrade.RealizedProfitLoss"/>; 0 when none.</param>
/// <param name="Wins">Trades with realized P/L above $0.00.</param>
/// <param name="Losses">Trades with realized P/L below $0.00. A $0.00 trade is neither.</param>
/// <param name="WinRate">Wins ÷ (Wins + Losses) × 100, 2 decimals; null when both are 0.</param>
/// <param name="LargestWin">Largest positive realized P/L; null when no wins.</param>
/// <param name="LargestLoss">Most negative realized P/L; null when no losses.</param>
/// <param name="ExcludedIncompleteCost">EQUITY/CRYPTO sales in the range left out because their cost history is incomplete (AC 8).</param>
/// <param name="ExcludedUnsupported">Filled orders in the range that are never valued: options, shorts, and any other instrument type.</param>
public record WebullTradeSummary(
    decimal TotalRealizedProfitLoss,
    int Wins,
    int Losses,
    decimal? WinRate,
    decimal? LargestWin,
    decimal? LargestLoss,
    int ExcludedIncompleteCost,
    int ExcludedUnsupported);

/// <summary><c>GET /api/webull/trades?range=</c>. Read-only and never budget data. Makes no Webull call.</summary>
/// <param name="History">Sync progress, As of and last error.</param>
/// <param name="From">First US-Eastern date in the range, inclusive; null for All.</param>
/// <param name="To">Today in US-Eastern time, inclusive.</param>
/// <param name="Summary">Figures over <paramref name="Trades"/>, plus the excluded counts.</param>
/// <param name="Trades">Closed trades in the range, newest first. Empty means "No closed trades in this range".</param>
public record WebullTradesView(
    WebullTradeHistoryStatus History,
    DateOnly? From,
    DateOnly To,
    WebullTradeSummary Summary,
    IReadOnlyList<WebullClosedTrade> Trades);
