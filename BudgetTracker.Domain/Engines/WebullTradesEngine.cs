using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Engines;

/// <summary>
/// Pure realized-P/L computation, average-cost method, before fees. Replays every counted order per (account, symbol) on
/// each read and keeps only the range's closed trades. The only place that reads Webull's order statuses, sides and
/// instrument types. No I/O.
/// </summary>
public class WebullTradesEngine : IWebullTradesEngine
{
    private const decimal QuantityStep = 0.00001m;

    private static readonly HashSet<string> CountedStatuses = ["FILLED", "PARTIAL_FILLED"];

    /// <summary>
    /// Valued instrument types and the type a trade reports. Webull's docs use both <c>STOCK</c> and <c>EQUITY</c> for
    /// shares, so both are stock and share one position.
    /// </summary>
    private static readonly Dictionary<string, string> ValuedTypes = new()
    {
        ["EQUITY"] = "EQUITY",
        ["STOCK"] = "EQUITY",
        ["CRYPTO"] = "CRYPTO"
    };

    private sealed class Position
    {
        public decimal Quantity;
        public decimal Cost;
        public bool CostUnknown;
        public int Fills;

        public void Reset() => (Quantity, Cost, CostUnknown, Fills) = (0m, 0m, false, 0);
    }

    /// <inheritdoc />
    public WebullTradesView BuildView(WebullTradeHistoryStatus history, IReadOnlyList<WebullOrder> orders, WebullRange range, DateTime now)
    {
        var to = EasternTime.ToEasternDate(now);
        var from = EasternTime.RangeStart(range, to);
        if (history.State == WebullState.NotSetUp)
            return new WebullTradesView(history, from, to, Summarize([], 0, 0), []);

        bool InRange(DateOnly date) => (from is null || date >= from) && date <= to;

        var counted = orders.Where(o => CountedStatuses.Contains(o.Status) && o.FilledQuantity > 0m).ToList();
        var valued = counted
            .Where(IsValued)
            .OrderBy(FillTime)
            .ThenBy(o => o.PlacedAt)
            .ThenBy(o => o.Side == "BUY" ? 0 : 1)
            .ThenBy(o => o.OrderId, StringComparer.Ordinal)
            .ToList();
        var excludedUnsupported = counted.Count(o => !IsValued(o) && InRange(CloseDate(o)));

        var positions = new Dictionary<(string, string), Position>();
        var trades = new List<(DateTime FillTime, WebullClosedTrade Trade)>();
        var excludedIncompleteCost = 0;
        foreach (var order in valued)
        {
            if (!positions.TryGetValue((order.AccountId, order.Symbol), out var position))
                positions[(order.AccountId, order.Symbol)] = position = new Position();

            var quantity = order.FilledQuantity!.Value;
            if (order.Side == "BUY")
            {
                position.Quantity += quantity;
                position.Cost += quantity * (order.FilledPrice ?? 0m);
                position.CostUnknown |= order.FilledPrice is not > 0m;
                position.Fills++;
                continue;
            }

            var closeDate = CloseDate(order);
            var tolerance = QuantityStep * position.Fills;
            if (quantity > position.Quantity + tolerance)
            {
                // Oversold: some earlier buy is missing, so nothing about this position can be trusted.
                position.Reset();
                if (InRange(closeDate))
                    excludedIncompleteCost++;
                continue;
            }

            var averageCost = position.Cost / position.Quantity;
            if (position.CostUnknown || order.FilledPrice is not > 0m)
            {
                if (InRange(closeDate))
                    excludedIncompleteCost++;
            }
            else if (InRange(closeDate))
            {
                var price = order.FilledPrice.Value;
                trades.Add((FillTime(order), new WebullClosedTrade(
                    order.AccountId,
                    order.OrderId,
                    closeDate,
                    order.Symbol,
                    ValuedTypes[order.InstrumentType!],
                    quantity,
                    averageCost,
                    price,
                    Math.Round((price - averageCost) * quantity, 2, MidpointRounding.AwayFromZero),
                    averageCost == 0m ? null : Round2((price - averageCost) / averageCost * 100m))));
            }

            position.Quantity -= quantity;
            position.Cost -= averageCost * quantity;
            position.Fills++;
            if (position.Quantity <= tolerance)
                position.Reset();
        }

        var sorted = trades
            .OrderByDescending(t => t.FillTime)
            .ThenBy(t => t.Trade.OrderId, StringComparer.Ordinal)
            .Select(t => t.Trade)
            .ToList();
        return new WebullTradesView(history, from, to, Summarize(sorted, excludedIncompleteCost, excludedUnsupported), sorted);
    }

    private static WebullTradeSummary Summarize(IReadOnlyList<WebullClosedTrade> trades, int excludedIncompleteCost, int excludedUnsupported)
    {
        var wins = trades.Where(t => t.RealizedProfitLoss > 0m).Select(t => t.RealizedProfitLoss).ToList();
        var losses = trades.Where(t => t.RealizedProfitLoss < 0m).Select(t => t.RealizedProfitLoss).ToList();
        var decided = wins.Count + losses.Count;

        return new WebullTradeSummary(
            trades.Sum(t => t.RealizedProfitLoss),
            wins.Count,
            losses.Count,
            decided == 0 ? null : Round2((decimal)wins.Count / decided * 100m),
            wins.Count == 0 ? null : wins.Max(),
            losses.Count == 0 ? null : losses.Min(),
            excludedIncompleteCost,
            excludedUnsupported);
    }

    private static bool IsValued(WebullOrder order) =>
        order.InstrumentType is { } type && ValuedTypes.ContainsKey(type) && order.Side is "BUY" or "SELL";

    private static DateTime FillTime(WebullOrder order) => order.FilledAt ?? order.PlacedAt;

    private static DateOnly CloseDate(WebullOrder order) => EasternTime.ToEasternDate(FillTime(order));

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
