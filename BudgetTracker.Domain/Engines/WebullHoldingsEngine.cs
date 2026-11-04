using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Engines;

/// <summary>
/// Pure holdings valuation for the Investments page. Values EQUITY and CRYPTO rows only; any missing input yields null,
/// never 0. The only place that reads Webull's <c>cost_price</c>.
/// </summary>
public class WebullHoldingsEngine : IWebullHoldingsEngine
{
    private static readonly HashSet<string> ValuedTypes = ["EQUITY", "CRYPTO"];

    /// <inheritdoc />
    public WebullHoldingsView BuildView(WebullConnectionView connection, IReadOnlyList<WebullAccountPortfolio> accounts) =>
        new(
            connection.State,
            connection.AsOf,
            connection.IsStale,
            connection.LastError,
            connection.State == WebullState.NotSetUp ? [] : accounts.Select(BuildAccount).ToList());

    private static WebullAccountHoldings BuildAccount(WebullAccountPortfolio account)
    {
        var rows = account.Positions.Select(Value).ToList();
        var totalMarketValue = rows.Sum(r => r.MarketValue ?? 0m);

        var holdings = rows
            .Select(r => r with
            {
                PercentOfHoldings = totalMarketValue == 0m ? null : Percent(r.MarketValue / totalMarketValue)
            })
            .OrderBy(r => r.MarketValue is null)
            .ThenByDescending(r => r.MarketValue)
            .ThenBy(r => r.Symbol, StringComparer.Ordinal)
            .ToList();

        var balance = account.Balance;
        return new WebullAccountHoldings(
            balance.AccountId,
            balance.AccountLabel,
            balance.AccountType,
            balance.Currency,
            balance.TotalNetLiquidationValue,
            balance.TotalCashBalance,
            account.TotalDayProfitLoss,
            account.TotalUnrealizedProfitLoss,
            AccountOpenPercent(account.TotalUnrealizedProfitLoss, holdings),
            holdings);
    }

    /// <summary>
    /// The account's open return: open P/L over the summed cost of its positions. Null unless every position has a cost,
    /// since a partial denominator would overstate the return.
    /// </summary>
    private static decimal? AccountOpenPercent(decimal? openProfitLoss, IReadOnlyList<WebullHolding> holdings)
    {
        if (openProfitLoss is null || holdings.Count == 0 || holdings.Any(h => h.TotalCost is null))
            return null;

        var totalCost = holdings.Sum(h => h.TotalCost!.Value);
        return totalCost == 0m ? null : Percent(openProfitLoss / totalCost);
    }

    private static WebullHolding Value(WebullPosition position)
    {
        if (!ValuedTypes.Contains(position.InstrumentType))
            return new WebullHolding(position.Symbol, position.InstrumentType, position.Quantity, null, null, null, null, null, null);

        // cost_price is per share: confirmed against production on 2026-10-25 (UPL ≈ Q × (last − cost) within $9–21; the
        // residual is Webull pricing UPL 0.06–0.16% above last_price, and we show Webull's UPL as-is).
        var averageCost = position.CostPrice;
        var totalCost = position.Quantity * position.CostPrice;
        return new WebullHolding(
            position.Symbol,
            position.InstrumentType,
            position.Quantity,
            averageCost,
            totalCost,
            position.Quantity * position.LastPrice,
            position.UnrealizedProfitLoss,
            totalCost == 0m ? null : Percent(position.UnrealizedProfitLoss / totalCost),
            null);
    }

    private static decimal? Percent(decimal? ratio) =>
        ratio is { } r ? Math.Round(r * 100m, 2, MidpointRounding.AwayFromZero) : null;
}
