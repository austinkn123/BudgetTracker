using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>
/// Webull's List Order History API (<c>GET /trading/orders/historical-orders/list</c>, x-version v3), read-only.
/// Implemented by <c>WebullAccessor</c> alongside <see cref="IWebullAccessor"/>, sharing its signer, allowlist and error
/// classification; this endpoint becomes the sixth allowlist entry. A separate contract so the trade-history caller can never
/// reach <see cref="IWebullAccessor.CreateTokenAsync"/> (which texts an SMS), and because order paging changes for different
/// reasons than the portfolio calls. Never persists, never decides which orders count.
/// </summary>
public interface IWebullOrderHistoryAccessor
{
    /// <summary>
    /// One page of <paramref name="accountId"/>'s orders in <paramref name="window"/>.
    /// <list type="bullet">
    /// <item>Query: <c>account_id</c>, <c>start_time</c> and <c>end_time</c> formatted <c>yyyy-MM-dd'T'HH:mm:ss.fff'Z'</c>, and
    /// <c>pagination_key</c> when the window has one. Every query parameter is signed (raw value) and URL-encoded in the request.
    /// The window is re-sent unchanged with the key: the key encodes a position, not the window.</item>
    /// <item>Waits until at least 2.1 s after this instance's previous call. Webull documents 2 requests per 2 s per endpoint,
    /// but a 1.1 s spacing still got a 429 in production; whether the portfolio endpoints share the budget is unknown.</item>
    /// <item>Response: <c>{ data: [ { combo_type, orders: [ … ] } ], pagination_key }</c>. Every <c>orders[]</c> entry of every
    /// group is returned, in any status, with <see cref="WebullOrder.AccountId"/> set to <paramref name="accountId"/>. A missing
    /// <c>data</c> is an empty page. A missing or blank <c>pagination_key</c> means last page (null).</item>
    /// <item>Unknown status, side or instrument values are kept as sent, never rejected. A missing <c>order_id</c>, <c>symbol</c>,
    /// <c>side</c>, <c>status</c> or placement time, or a number field that is present but not a number, is an unreadable
    /// response. <c>place_time_at</c> falls back to the epoch-ms <c>place_time</c>; <c>filled_time_at</c> to <c>filled_time</c>.</item>
    /// <item>Every failure throws <see cref="WebullApiException"/>; not configured, or a request off the allowlist, throws
    /// <see cref="InvalidOperationException"/> without sending.</item>
    /// </list>
    /// </summary>
    Task<WebullOrderPage> GetOrderPageAsync(
        string accessToken,
        string accountId,
        WebullOrderWindow window,
        CancellationToken cancellationToken = default);
}
