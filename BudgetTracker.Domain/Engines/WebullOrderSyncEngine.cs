using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Engines;

/// <summary>
/// Pure order-history sync planning: which window to fetch next and how a page or a failure moves an account's progress.
/// Backfill windows never cross a UTC calendar year and stop at Webull's 2018-05-21 floor. No I/O.
/// </summary>
public class WebullOrderSyncEngine : IWebullOrderSyncEngine
{
    private static readonly DateTime Floor = new(2018, 5, 21, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan IncrementalAfter = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Overlap = TimeSpan.FromDays(7);
    private static readonly TimeSpan OpenOrderLookback = TimeSpan.FromDays(90);
    private static readonly HashSet<string> OpenStatuses = ["PENDING", "SUBMITTED", "PARTIAL_FILLED"];

    /// <inheritdoc />
    public WebullOrderWindow? NextWindow(WebullOrderSync sync, IReadOnlyList<WebullOrder> accountOrders, DateTime now)
    {
        if (sync.Window is { } inProgress)
            return inProgress;

        if (sync.CoveredFrom is not { } coveredFrom || sync.CoveredTo is not { } coveredTo)
            return new WebullOrderWindow(Max(Floor, YearStart(now)), now, null);

        if (now - coveredTo >= IncrementalAfter)
        {
            var start = (accountOrders.Count == 0 ? coveredTo : accountOrders.Max(o => o.PlacedAt)) - Overlap;
            var oldestOpen = accountOrders
                .Where(o => OpenStatuses.Contains(o.Status) && o.PlacedAt >= now - OpenOrderLookback)
                .Min(o => (DateTime?)o.PlacedAt);
            return new WebullOrderWindow(Max(Floor, oldestOpen < start ? oldestOpen.Value : start), now, null);
        }

        if (coveredFrom > Floor)
        {
            // The last whole millisecond before CoveredFrom: Webull's times are whole milliseconds.
            var end = new DateTime((coveredFrom.Ticks - 1) / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
            return new WebullOrderWindow(Max(Floor, YearStart(end)), end, null);
        }

        return null;
    }

    /// <inheritdoc />
    public WebullOrderSync Advance(WebullOrderSync sync, WebullOrderWindow window, string? nextPaginationKey) =>
        string.IsNullOrWhiteSpace(nextPaginationKey)
            ? sync with
            {
                CoveredFrom = sync.CoveredFrom < window.Start ? sync.CoveredFrom : window.Start,
                CoveredTo = sync.CoveredTo > window.End ? sync.CoveredTo : window.End,
                Window = null,
                LastError = null
            }
            : sync with { Window = window with { PaginationKey = nextPaginationKey }, LastError = null };

    /// <inheritdoc />
    public WebullOrderSync Fail(WebullOrderSync sync, WebullErrorKind error) =>
        sync with { Window = sync.Window is { } window ? window with { PaginationKey = null } : null, LastError = error };

    /// <inheritdoc />
    public WebullTradeHistoryStatus BuildStatus(WebullState state, IReadOnlyList<string> accountIds, IReadOnlyList<WebullOrderSync> syncs)
    {
        var current = accountIds
            .Select(id => syncs.FirstOrDefault(s => s.AccountId == id) ?? new WebullOrderSync(id, null, null, null, null))
            .ToList();
        var allCovered = current.All(s => s.CoveredFrom is not null && s.CoveredTo is not null);

        return new WebullTradeHistoryStatus(
            state,
            allCovered ? current.Min(s => s.CoveredTo) : null,
            allCovered && current.Max(s => s.CoveredFrom) is { } newestFrom ? EasternTime.ToEasternDate(newestFrom) : null,
            current.All(s => s.CoveredFrom <= Floor),
            current.FirstOrDefault(s => s.LastError is not null)?.LastError);
    }

    private static DateTime YearStart(DateTime utc) => new(utc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
