namespace BudgetTracker.Domain.Webull;

/// <summary>
/// One position exactly as Webull's <c>GET /trading/assets/positions/list</c> reports it. Stored unchanged. What
/// <see cref="CostPrice"/> means (per share or total) is decided only by
/// <see cref="Interfaces.Engines.IWebullHoldingsEngine"/>, so fixing that reading never needs a data migration.
/// A field Webull sends empty or omits is null, never 0.
/// </summary>
/// <param name="Symbol">Webull's <c>symbol</c>.</param>
/// <param name="InstrumentType">Webull's <c>instrument_type</c> as sent (<c>EQUITY</c>, <c>CRYPTO</c>, <c>OPTION</c>, <c>FUTURES</c>, <c>EVENT</c>).</param>
/// <param name="Quantity">Webull's <c>quantity</c>. Fractional for crypto.</param>
/// <param name="LastPrice">Webull's <c>last_price</c>. Not documented as live; label it "as of" the fetch.</param>
/// <param name="CostPrice">Webull's raw <c>cost_price</c>: the average cost per share (confirmed against production on 2026-10-25).</param>
/// <param name="UnrealizedProfitLoss">Webull's <c>unrealized_profit_loss</c>: the open G/L in dollars.</param>
/// <param name="Currency">Webull's <c>currency</c>.</param>
public record WebullPosition(
    string Symbol,
    string InstrumentType,
    decimal? Quantity,
    decimal? LastPrice,
    decimal? CostPrice,
    decimal? UnrealizedProfitLoss,
    string Currency);

/// <summary>
/// One account from a single fetch: its v1 balance, the three balance totals v1 discarded, and its positions. What the
/// Webull accessor returns and what the connection accessor saves, so balances and positions always share one fetch time.
/// </summary>
/// <param name="Balance">The v1 balance (total value, cash, currency).</param>
/// <param name="TotalMarketValue">Webull's <c>total_market_value</c>, or null when empty.</param>
/// <param name="TotalDayProfitLoss">Webull's <c>total_day_profit_loss</c>, or null when empty.</param>
/// <param name="TotalUnrealizedProfitLoss">Webull's <c>total_unrealized_profit_loss</c> (open P/L), or null when empty.</param>
/// <param name="Positions">Every position Webull listed for the account; empty when it holds none.</param>
public record WebullAccountPortfolio(
    WebullBalance Balance,
    decimal? TotalMarketValue,
    decimal? TotalDayProfitLoss,
    decimal? TotalUnrealizedProfitLoss,
    IReadOnlyList<WebullPosition> Positions);

/// <summary>
/// One row of the Investments table, computed by <see cref="Interfaces.Engines.IWebullHoldingsEngine"/>. Every computed
/// value is null (shown as "—", never $0) when an input is missing or the instrument is not EQUITY or CRYPTO.
/// Percentages are in percent units rounded to 2 decimals (12.34 means 12.34%).
/// </summary>
/// <param name="Symbol">Webull's symbol.</param>
/// <param name="InstrumentType">Webull's instrument type as sent.</param>
/// <param name="Quantity">Webull's quantity.</param>
/// <param name="AverageCost">Average cost per share.</param>
/// <param name="TotalCost">Total cost basis.</param>
/// <param name="MarketValue">Quantity × last price.</param>
/// <param name="OpenProfitLoss">Webull's unrealized P/L in dollars.</param>
/// <param name="OpenProfitLossPercent">OpenProfitLoss ÷ TotalCost, in percent.</param>
/// <param name="PercentOfHoldings">This row's share of the account's positions' market value (cash excluded), in percent.</param>
public record WebullHolding(
    string Symbol,
    string InstrumentType,
    decimal? Quantity,
    decimal? AverageCost,
    decimal? TotalCost,
    decimal? MarketValue,
    decimal? OpenProfitLoss,
    decimal? OpenProfitLossPercent,
    decimal? PercentOfHoldings);

/// <summary>One account section of the Investments page: the header figures from the balance, then its holdings.</summary>
/// <param name="AccountId">Webull's account id.</param>
/// <param name="AccountLabel">Webull's account label, or null.</param>
/// <param name="AccountType">Webull's account type.</param>
/// <param name="Currency">The account's currency.</param>
/// <param name="TotalValue">Webull's total net liquidation value.</param>
/// <param name="Cash">Webull's total cash balance, or null.</param>
/// <param name="DayProfitLoss">Webull's total day P/L, or null.</param>
/// <param name="OpenProfitLoss">Webull's total unrealized P/L, or null.</param>
/// <param name="OpenProfitLossPercent">
/// <paramref name="OpenProfitLoss"/> ÷ the sum of the positions' TotalCost, in percent rounded to 2 decimals. Null when
/// OpenProfitLoss is null, there are no positions, any position has no TotalCost, or the summed cost is 0.
/// </param>
/// <param name="Positions">Sorted by market value, descending; rows with no market value last. Empty means "No positions".</param>
public record WebullAccountHoldings(
    string AccountId,
    string? AccountLabel,
    string AccountType,
    string Currency,
    decimal TotalValue,
    decimal? Cash,
    decimal? DayProfitLoss,
    decimal? OpenProfitLoss,
    decimal? OpenProfitLossPercent,
    IReadOnlyList<WebullHolding> Positions);

/// <summary>
/// <c>GET /api/webull/holdings</c>. Read-only and never budget data. <see cref="AsOf"/> is the same fetch time as
/// <see cref="WebullConnectionView.AsOf"/>, because balances and positions are saved together.
/// </summary>
/// <param name="State">Same state as <see cref="WebullConnectionView.State"/>.</param>
/// <param name="AsOf">When the saved balances and positions were fetched (UTC), or null when never fetched.</param>
/// <param name="IsStale">True when <paramref name="AsOf"/> is more than 24 hours old.</param>
/// <param name="LastError">Why the most recent Webull call failed, or null.</param>
/// <param name="Accounts">The last saved fetch, one section per account; empty when NotSetUp or never fetched.</param>
public record WebullHoldingsView(
    WebullState State,
    DateTime? AsOf,
    bool IsStale,
    WebullErrorKind? LastError,
    IReadOnlyList<WebullAccountHoldings> Accounts);
