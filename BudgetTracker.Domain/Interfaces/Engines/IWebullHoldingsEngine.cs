using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>
/// Pure holdings valuation. No I/O. The only place that knows what Webull's position fields mean, including that
/// <c>cost_price</c> is per share (confirmed against production on 2026-10-25). Separate from <see cref="IWebullEngine"/>, which owns link state and refresh timing: Webull's field
/// semantics change for different reasons than its token rules.
/// </summary>
public interface IWebullHoldingsEngine
{
    // ponytail: 2-decimal rounding keeps the % column within ±0.1 of 100 up to 20 rows per account. Past that, use largest-remainder rounding.
    /// <summary>
    /// Build the Investments view from the connection view and the saved accounts.
    /// <list type="bullet">
    /// <item><see cref="WebullState.NotSetUp"/>: no accounts. Every other state shows whatever is saved.</item>
    /// <item>State, AsOf, IsStale and LastError are copied from <paramref name="connection"/>.</item>
    /// <item>Only EQUITY and CRYPTO rows get computed values. Any other type is a plain row: symbol, type, quantity,
    /// with every computed value null.</item>
    /// <item>MarketValue = Quantity × LastPrice. OpenProfitLoss = Webull's unrealized P/L. OpenProfitLossPercent =
    /// OpenProfitLoss ÷ TotalCost × 100. Null when an input is null or TotalCost is 0.</item>
    /// <item>PercentOfHoldings = MarketValue ÷ the sum of the account's non-null MarketValues × 100, cash excluded. Rows
    /// with values sum to 100 ± 0.1.</item>
    /// <item>Percentages are rounded to 2 decimals, away from zero. Money is not rounded.</item>
    /// <item>Rows are sorted by MarketValue descending, nulls last, then by Symbol.</item>
    /// <item>Accounts keep the order of <paramref name="accounts"/>.</item>
    /// </list>
    /// </summary>
    /// <param name="connection">The v1 view from <see cref="IWebullEngine.BuildView"/>.</param>
    /// <param name="accounts">The last saved fetch; empty when none.</param>
    WebullHoldingsView BuildView(WebullConnectionView connection, IReadOnlyList<WebullAccountPortfolio> accounts);
}
