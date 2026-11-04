using System.Globalization;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Tests.Engines;

/// <summary>
/// Tests for <see cref="WebullTradesEngine"/>: which orders count and which are excluded, the average-cost replay per
/// (account, symbol) with its tie-breaks, oversells and the 5-decimal tolerance, unknown cost, the Eastern-date range filter
/// over earlier cost history, cent rounding (a $0.00 trade is neither a win nor a loss), sorting and the summary.
/// </summary>
public class WebullTradesEngineTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T0 = new(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
    private static readonly WebullTradeHistoryStatus History =
        new(WebullState.Connected, Now.AddMinutes(-3), new DateOnly(2018, 5, 21), true, null);

    private readonly WebullTradesEngine _sut = new();

    private static DateTime At(double hours) => T0.AddHours(hours);

    private static WebullOrder Order(
        string id, string side, decimal? quantity, decimal? price, DateTime filledAt,
        string symbol = "AAPL", string? type = "EQUITY", string status = "FILLED", string account = "ACC1") =>
        new(account, id, symbol, type, side, status, quantity, price, filledAt, filledAt);

    private static WebullOrder Buy(string id, decimal quantity, decimal? price, double hour, string symbol = "AAPL", string type = "EQUITY", string account = "ACC1") =>
        Order(id, "BUY", quantity, price, At(hour), symbol, type, account: account);

    private static WebullOrder Sell(string id, decimal quantity, decimal? price, double hour, string symbol = "AAPL", string type = "EQUITY", string account = "ACC1") =>
        Order(id, "SELL", quantity, price, At(hour), symbol, type, account: account);

    private WebullTradesView View(IReadOnlyList<WebullOrder> orders, WebullRange range = WebullRange.YearToDate) =>
        _sut.BuildView(History, orders, range, Now);

    private WebullClosedTrade SingleTrade(params WebullOrder[] orders) => Assert.Single(View(orders).Trades);

    // ── View header ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(WebullRange.OneMonth, "2026-09-25")]
    [InlineData(WebullRange.ThreeMonths, "2026-07-25")]
    [InlineData(WebullRange.YearToDate, "2026-01-01")]
    [InlineData(WebullRange.OneYear, "2025-10-25")]
    [InlineData(WebullRange.All, null)]
    public void BuildView_CopiesHistory_AndRangeRunsToTodayInEasternTime(WebullRange range, string? from)
    {
        var view = _sut.BuildView(History, [], range, new DateTime(2026, 10, 26, 3, 59, 0, DateTimeKind.Utc)); // 23:59 EDT Oct 25

        Assert.Equal(History, view.History);
        Assert.Equal(from is null ? null : DateOnly.Parse(from, CultureInfo.InvariantCulture), view.From);
        Assert.Equal(new DateOnly(2026, 10, 25), view.To);
    }

    [Fact]
    public void BuildView_NotSetUp_NoTradesAndAZeroSummary()
    {
        var notSetUp = History with { State = WebullState.NotSetUp };

        var view = _sut.BuildView(notSetUp, [Buy("B", 10, 100, 0), Sell("S", 10, 120, 1), Sell("X", 1, 1, 2)], WebullRange.All, Now);

        Assert.Equal(notSetUp, view.History);
        Assert.Empty(view.Trades);
        Assert.Equal(new WebullTradeSummary(0m, 0, 0, null, null, null, 0, 0), view.Summary);
    }

    // ── Happy path and average cost ─────────────────────────────────────────

    [Fact]
    public void Sell_RealizesAgainstAverageCost()
    {
        var trade = SingleTrade(Buy("B1", 10, 100m, 0), Sell("S1", 4, 120m, 1));

        Assert.Equal(new WebullClosedTrade("ACC1", "S1", new DateOnly(2026, 6, 1), "AAPL", "EQUITY", 4m, 100m, 120m, 80m, 20m), trade);
    }

    [Fact]
    public void Buys_AverageTogether_AndASaleLeavesTheAverageUnchanged()
    {
        var trades = View([Buy("B1", 10, 100m, 0), Buy("B2", 10, 110m, 1), Sell("S1", 5, 120m, 2), Sell("S2", 5, 100m, 3)]).Trades;

        Assert.Equal([(105m, 75m), (105m, -25m)], trades.OrderBy(t => t.OrderId).Select(t => (t.AverageCost, t.RealizedProfitLoss)));
    }

    [Fact]
    public void BuyAfterPartialSale_AveragesWithTheRemainingCost()
    {
        // 5 left at 100 (cost 500) + 5 at 130 = cost 1150 over 10 shares.
        var trades = View([Buy("B1", 10, 100m, 0), Sell("S1", 5, 120m, 1), Buy("B2", 5, 130m, 2), Sell("S2", 10, 120m, 3)]).Trades;

        Assert.Equal(115m, trades.Single(t => t.OrderId == "S2").AverageCost);
        Assert.Equal(50m, trades.Single(t => t.OrderId == "S2").RealizedProfitLoss);
    }

    [Fact]
    public void AverageCost_IsNotRounded()
    {
        var trade = SingleTrade(Buy("B1", 1, 3m, 0), Buy("B2", 1, 3m, 1), Buy("B3", 1, 4m, 2), Sell("S1", 3, 4m, 3));

        Assert.Equal(10m / 3m, trade.AverageCost);
        Assert.Equal(2m, trade.RealizedProfitLoss); // (4 − 3.333…) × 3 = 2.000… rounded to cents
    }

    [Fact]
    public void FractionalCrypto_IsValuedLikeStock()
    {
        // Cost 0.0123 × 50000 + 0.0077 × 60000 = 615 + 462 = 1077 over 0.02 BTC: average 53850.
        var trade = SingleTrade(
            Buy("B1", 0.0123m, 50000m, 0, "BTCUSD", "CRYPTO"),
            Buy("B2", 0.0077m, 60000m, 1, "BTCUSD", "CRYPTO"),
            Sell("S1", 0.015m, 65000m, 2, "BTCUSD", "CRYPTO"));

        Assert.Equal(new WebullClosedTrade("ACC1", "S1", new DateOnly(2026, 6, 1), "BTCUSD", "CRYPTO", 0.015m, 53850m, 65000m, 167.25m, 20.71m), trade);
    }

    [Fact]
    public void Positions_AreTrackedPerAccountAndSymbol()
    {
        var trades = View(
        [
            Buy("B1", 10, 100m, 0, account: "ACC1"),
            Buy("B2", 10, 200m, 1, account: "ACC2"),
            Buy("B3", 10, 50m, 2, symbol: "MSFT"),
            Sell("S1", 10, 150m, 3, account: "ACC1"),
            Sell("S2", 10, 150m, 4, account: "ACC2"),
            Sell("S3", 10, 60m, 5, symbol: "MSFT")
        ]).Trades;

        Assert.Equal(
            [("S1", 100m, 500m), ("S2", 200m, -500m), ("S3", 50m, 100m)],
            trades.OrderBy(t => t.OrderId).Select(t => (t.OrderId, t.AverageCost, t.RealizedProfitLoss)));
    }

    [Fact]
    public void CloseDate_IsTheEasternDateOfTheFill()
    {
        var sale = Order("S1", "SELL", 1, 120m, new DateTime(2026, 3, 8, 3, 0, 0, DateTimeKind.Utc)); // 22:00 EST Mar 7

        var trade = SingleTrade(Buy("B1", 1, 100m, -24 * 120), sale);

        Assert.Equal(new DateOnly(2026, 3, 7), trade.CloseDate);
    }

    // ── Which orders count ──────────────────────────────────────────────────

    [Fact]
    public void PartialFilled_CountsWithItsFilledQuantity()
    {
        var partialBuy = Buy("B1", 6, 100m, 0) with { Status = "PARTIAL_FILLED" };
        var partialSell = Sell("S1", 2, 110m, 1) with { Status = "PARTIAL_FILLED" };

        var trade = SingleTrade(partialBuy, partialSell);

        Assert.Equal((2m, 100m, 20m), (trade.Quantity, trade.AverageCost, trade.RealizedProfitLoss));
    }

    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("PENDING")]
    [InlineData("SUBMITTED")]
    [InlineData("FAILED")]
    [InlineData("filled")]
    public void OtherStatuses_AreIgnored_NeverExcluded(string status)
    {
        var view = View(
        [
            Buy("B1", 10, 100m, 0),
            Buy("B2", 10, 1m, 1) with { Status = status }, // would drag the average down if it counted
            Sell("S1", 10, 120m, 2),
            Sell("S2", 50, 120m, 3) with { Status = status }, // would be an oversell if it counted
            Sell("S3", 5, 1m, 4, type: "OPTION") with { Status = status }
        ]);

        Assert.Equal(100m, Assert.Single(view.Trades).AverageCost);
        Assert.Equal((0, 0), (view.Summary.ExcludedIncompleteCost, view.Summary.ExcludedUnsupported));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void ZeroOrMissingFilledQuantity_IsIgnored_NeverExcluded(int? quantity)
    {
        var filledQuantity = (decimal?)quantity;
        var view = View(
        [
            Buy("B1", 10, 100m, 0),
            Buy("B2", 1, 1m, 1) with { FilledQuantity = filledQuantity },
            Sell("S1", 10, 120m, 2),
            Sell("S2", 1, 120m, 3) with { FilledQuantity = filledQuantity },
            Sell("S3", 1, 1m, 4, type: "OPTION") with { FilledQuantity = filledQuantity }
        ]);

        Assert.Equal(100m, Assert.Single(view.Trades).AverageCost);
        Assert.Equal((0, 0), (view.Summary.ExcludedIncompleteCost, view.Summary.ExcludedUnsupported));
    }

    [Fact]
    public void StockReportedAsStock_IsValuedAsEquity_AndSharesThePosition()
    {
        var trades = View([Buy("B1", 10, 100m, 0, type: "STOCK"), Buy("B2", 10, 120m, 1), Sell("S1", 5, 130m, 2, type: "STOCK")]).Trades;

        var trade = Assert.Single(trades);
        Assert.Equal(("EQUITY", 110m, 100m), (trade.InstrumentType, trade.AverageCost, trade.RealizedProfitLoss));
    }

    [Theory]
    [InlineData("OPTION", "BUY")]
    [InlineData("OPTION", "SELL")]
    [InlineData("FUTURES", "SELL")]
    [InlineData("EVENT", "BUY")]
    [InlineData(null, "SELL")]
    [InlineData("equity", "SELL")]
    [InlineData("EQUITY", "SHORT")]
    [InlineData("CRYPTO", "SHORT")]
    public void UnsupportedInstrumentOrSide_IsExcludedUnsupported_AndNeverTouchesThePosition(string? type, string side)
    {
        var view = View(
        [
            Buy("B1", 10, 100m, 0),
            Order("X1", side, 10, 1m, At(1), type: type), // same symbol: must not change AAPL's quantity or cost
            Sell("S1", 10, 120m, 2)
        ]);

        Assert.Equal((100m, 200m), (Assert.Single(view.Trades).AverageCost, view.Trades[0].RealizedProfitLoss));
        Assert.Equal((0, 1), (view.Summary.ExcludedIncompleteCost, view.Summary.ExcludedUnsupported));
    }

    [Fact]
    public void Unsupported_OutsideTheRange_IsNotCounted()
    {
        var view = View([Order("X1", "SELL", 1, 1m, new DateTime(2025, 12, 31, 12, 0, 0, DateTimeKind.Utc), type: "OPTION")]);

        Assert.Equal(0, view.Summary.ExcludedUnsupported);
    }

    // ── Replay order and tie-breaks ─────────────────────────────────────────

    [Fact]
    public void Replay_IsByFillTime_WhateverTheInputOrder()
    {
        var trade = SingleTrade(Sell("S1", 10, 120m, 5), Buy("B2", 10, 200m, 9), Buy("B1", 10, 100m, 1));

        Assert.Equal(100m, trade.AverageCost);
    }

    [Fact]
    public void Replay_UsesFillTimeNotPlacement()
    {
        // The sale was placed first but filled after the buy.
        var buy = Buy("B1", 10, 100m, 2) with { PlacedAt = At(1) };
        var sale = Sell("S1", 10, 120m, 3) with { PlacedAt = At(0) };

        Assert.Equal(200m, SingleTrade(sale, buy).RealizedProfitLoss);
    }

    [Fact]
    public void Replay_WithoutFillTime_FallsBackToPlacement()
    {
        var buy = Buy("B1", 10, 100m, 0);
        var sale = Sell("S1", 10, 120m, 9) with { PlacedAt = At(1), FilledAt = null };
        var laterBuy = Buy("B2", 10, 500m, 2);

        var trade = SingleTrade(laterBuy, sale, buy);

        Assert.Equal((100m, new DateOnly(2026, 6, 1)), (trade.AverageCost, trade.CloseDate));
    }

    [Fact]
    public void Replay_SameFillTime_EarlierPlacementFirst()
    {
        // Placed before the buy, so it is replayed first: an oversell from an empty position.
        var sale = Sell("S1", 10, 120m, 1) with { PlacedAt = At(-2) };
        var buy = Buy("B1", 10, 100m, 1) with { PlacedAt = At(-1) };

        var view = View([buy, sale]);

        Assert.Empty(view.Trades);
        Assert.Equal(1, view.Summary.ExcludedIncompleteCost);
    }

    [Fact]
    public void Replay_SameFillAndPlacement_BuyBeforeSell()
    {
        Assert.Equal(200m, SingleTrade(Sell("A", 10, 120m, 1), Buy("Z", 10, 100m, 1)).RealizedProfitLoss);
    }

    [Fact]
    public void Replay_FullTie_OrdinalOrderIdDecides()
    {
        // Ordinal: "B" (66) sorts before "a" (97); a culture-aware sort would put "a" first.
        var view = View([Buy("B0", 10, 100m, 0), Sell("a", 8, 120m, 1), Sell("B", 8, 130m, 1)]);

        Assert.Equal("B", Assert.Single(view.Trades).OrderId);
        Assert.Equal(1, view.Summary.ExcludedIncompleteCost);
    }

    // ── Oversell and tolerance ──────────────────────────────────────────────

    [Fact]
    public void Oversell_IsExcluded_AndResetsThePosition()
    {
        var view = View([Buy("B1", 5, 100m, 0), Sell("S1", 10, 120m, 1), Buy("B2", 5, 200m, 2), Sell("S2", 5, 210m, 3)]);

        var trade = Assert.Single(view.Trades);
        Assert.Equal(("S2", 200m, 50m), (trade.OrderId, trade.AverageCost, trade.RealizedProfitLoss));
        Assert.Equal(1, view.Summary.ExcludedIncompleteCost);
    }

    [Fact]
    public void SaleWithNoPosition_IsAnOversell()
    {
        var view = View([Sell("S1", 0.00001m, 120m, 0)]);

        Assert.Empty(view.Trades);
        Assert.Equal(1, view.Summary.ExcludedIncompleteCost);
    }

    [Theory]
    [InlineData("1.00001", true)] // exactly the tolerance of one fill
    [InlineData("1.00002", false)]
    public void Tolerance_OneFill_AllowsOneHundredThousandthOver(string saleQuantity, bool realized)
    {
        var view = View([Buy("B1", 1, 100m, 0), Sell("S1", decimal.Parse(saleQuantity, CultureInfo.InvariantCulture), 110m, 1)]);

        Assert.Equal(realized ? 1 : 0, view.Trades.Count);
        Assert.Equal(realized ? 0 : 1, view.Summary.ExcludedIncompleteCost);
    }

    [Theory]
    [InlineData("2.00002", true)]
    [InlineData("2.00003", false)]
    public void Tolerance_GrowsWithEachFillSinceEmpty(string saleQuantity, bool realized)
    {
        var view = View([Buy("B1", 1, 100m, 0), Buy("B2", 1, 100m, 1), Sell("S1", decimal.Parse(saleQuantity, CultureInfo.InvariantCulture), 110m, 2)]);

        Assert.Equal(realized ? 1 : 0, view.Trades.Count);
    }

    [Fact]
    public void Tolerance_CountsEarlierSalesAsFills()
    {
        // Fills before the last sale: the buy and the first sale, so t = 0.00002.
        var view = View([Buy("B1", 1, 100m, 0), Sell("S1", 0.5m, 110m, 1), Sell("S2", 0.50002m, 110m, 2)]);

        Assert.Equal(2, view.Trades.Count);
    }

    [Fact]
    public void Tolerance_RoundedFractionalBuys_SellTheWhole()
    {
        // Three thirds reported to 5 decimals sum to 0.99999.
        var view = View([Buy("B1", 0.33333m, 30m, 0), Buy("B2", 0.33333m, 30m, 1), Buy("B3", 0.33333m, 30m, 2), Sell("S1", 1m, 33m, 3)]);

        var trade = Assert.Single(view.Trades);
        Assert.Equal((30m, 3m), (trade.AverageCost, trade.RealizedProfitLoss));
    }

    [Fact]
    public void Tolerance_FillCountRestartsWhenThePositionEmpties()
    {
        // Two fills, then empty; one more fill gives t = 0.00001 again, so 1.00002 is an oversell.
        var view = View([Buy("B1", 1, 100m, 0), Buy("B2", 1, 100m, 1), Sell("S1", 2, 110m, 2), Buy("B3", 1, 100m, 3), Sell("S2", 1.00002m, 110m, 4)]);

        Assert.Equal("S1", Assert.Single(view.Trades).OrderId);
        Assert.Equal(1, view.Summary.ExcludedIncompleteCost);
    }

    [Fact]
    public void Remainder_AtTheTolerance_ResetsThePosition()
    {
        // 0.00001 left after the sale is dust: the position empties, so the next sale has nothing to sell.
        var view = View([Buy("B1", 1, 100m, 0), Sell("S1", 0.99999m, 110m, 1), Sell("S2", 0.00001m, 110m, 2)]);

        Assert.Equal("S1", Assert.Single(view.Trades).OrderId);
        Assert.Equal(1, view.Summary.ExcludedIncompleteCost);
    }

    [Fact]
    public void Remainder_AboveTheTolerance_StaysOpenAtTheSameAverage()
    {
        var view = View([Buy("B1", 1, 100m, 0), Sell("S1", 0.99998m, 110m, 1), Sell("S2", 0.00002m, 110m, 2)]);

        Assert.Equal([100m, 100m], view.Trades.Select(t => t.AverageCost));
        Assert.Equal(0, view.Summary.ExcludedIncompleteCost);
    }

    [Fact]
    public void Remainder_ResetClearsTheOldCost()
    {
        var view = View([Buy("B1", 1, 100m, 0), Sell("S1", 0.99999m, 110m, 1), Buy("B2", 1, 300m, 2), Sell("S2", 1, 310m, 3)]);

        Assert.Equal(300m, view.Trades.Single(t => t.OrderId == "S2").AverageCost);
    }

    // ── Unknown cost and missing prices ─────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public void BuyWithoutAPositivePrice_MakesCostUnknownUntilThePositionEmpties(int? price)
    {
        var view = View(
        [
            Buy("B1", 10, price, 0),
            Buy("B2", 10, 100m, 1),
            Sell("S1", 5, 120m, 2),  // excluded: cost unknown
            Sell("S2", 15, 120m, 3), // excluded, and empties the position
            Buy("B3", 10, 100m, 4),
            Sell("S3", 10, 120m, 5)
        ]);

        var trade = Assert.Single(view.Trades);
        Assert.Equal(("S3", 100m, 200m), (trade.OrderId, trade.AverageCost, trade.RealizedProfitLoss));
        Assert.Equal(2, view.Summary.ExcludedIncompleteCost);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void SaleWithoutAPositivePrice_IsExcluded_AndReducesThePositionAtTheAverage(int? price)
    {
        var view = View([Buy("B1", 10, 100m, 0), Sell("S1", 4, price, 1), Sell("S2", 6, 120m, 2), Sell("S3", 1, 120m, 3)]);

        var trade = Assert.Single(view.Trades);
        Assert.Equal(("S2", 6m, 100m, 120m), (trade.OrderId, trade.Quantity, trade.AverageCost, trade.RealizedProfitLoss));
        Assert.Equal(2, view.Summary.ExcludedIncompleteCost); // S1 (no price) and S3 (nothing left)
    }

    // ── Range filter over earlier cost history ──────────────────────────────

    [Fact]
    public void CostHistoryBeforeTheRange_StillCounts()
    {
        var view = View(
        [
            Order("B1", "BUY", 10, 100m, new DateTime(2024, 3, 1, 15, 0, 0, DateTimeKind.Utc)),
            Order("S0", "SELL", 5, 90m, new DateTime(2025, 6, 1, 15, 0, 0, DateTimeKind.Utc)),    // before YTD: affects the position only
            Order("S9", "SELL", 99, 90m, new DateTime(2025, 6, 2, 15, 0, 0, DateTimeKind.Utc)) with { Symbol = "MSFT" }, // oversell, before YTD
            Order("S1", "SELL", 5, 150m, new DateTime(2026, 2, 1, 15, 0, 0, DateTimeKind.Utc))
        ]);

        var trade = Assert.Single(view.Trades);
        Assert.Equal(("S1", 100m, 250m), (trade.OrderId, trade.AverageCost, trade.RealizedProfitLoss));
        Assert.Equal(0, view.Summary.ExcludedIncompleteCost);
    }

    [Theory]
    [InlineData(WebullRange.YearToDate, "2026-01-01T04:59:59Z", false)] // 23:59:59 EST Dec 31
    [InlineData(WebullRange.YearToDate, "2026-01-01T05:00:00Z", true)]  // 00:00 EST Jan 1
    [InlineData(WebullRange.OneMonth, "2026-09-25T03:59:59Z", false)]  // 23:59:59 EDT Sep 24
    [InlineData(WebullRange.OneMonth, "2026-09-25T04:00:00Z", true)]   // 00:00 EDT Sep 25
    [InlineData(WebullRange.All, "2018-05-21T15:00:00Z", true)]
    public void Range_IsInclusiveOfItsFirstEasternDate(WebullRange range, string fillUtc, bool included)
    {
        var fill = DateTime.Parse(fillUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        IReadOnlyList<WebullOrder> orders =
        [
            Order("B1", "BUY", 10, 100m, new DateTime(2018, 5, 21, 14, 0, 0, DateTimeKind.Utc)),
            Order("S1", "SELL", 5, 120m, fill),
            Order("S2", "SELL", 99, 120m, fill),                       // oversell on the same date
            Order("X1", "BUY", 1, 1m, fill, type: "OPTION")            // unsupported on the same date
        ];

        var view = _sut.BuildView(History, orders, range, Now);

        Assert.Equal(included ? 1 : 0, view.Trades.Count);
        Assert.Equal(included ? 1 : 0, view.Summary.ExcludedIncompleteCost);
        Assert.Equal(included ? 1 : 0, view.Summary.ExcludedUnsupported);
    }

    // ── Rounding and $0.00 ──────────────────────────────────────────────────

    [Theory]
    [InlineData("10.005", "0.01", "0.05")]   // half a cent rounds away from zero
    [InlineData("9.995", "-0.01", "-0.05")]
    [InlineData("10.004", "0.00", "0.04")]   // under half a cent: $0.00
    [InlineData("13.3333", "3.33", "33.33")]
    [InlineData("6.66666", "-3.33", "-33.33")]
    [InlineData("10.0005", "0.00", "0.01")]  // 0.005% rounds away from zero
    public void ProfitLoss_RoundsToCents_PercentToTwoDecimals_AwayFromZero(string salePrice, string profitLoss, string percent)
    {
        var trade = SingleTrade(Buy("B1", 1, 10m, 0), Sell("S1", 1, decimal.Parse(salePrice, CultureInfo.InvariantCulture), 1));

        Assert.Equal((decimal.Parse(profitLoss, CultureInfo.InvariantCulture), decimal.Parse(percent, CultureInfo.InvariantCulture)), (trade.RealizedProfitLoss, trade.RealizedProfitLossPercent));
    }

    [Fact]
    public void ZeroDollarTrade_IsNeitherWinNorLoss_ButIsListed()
    {
        var view = View([Buy("B1", 1, 10m, 0), Sell("S1", 1, 10.004m, 1)]);

        Assert.Single(view.Trades);
        Assert.Equal(new WebullTradeSummary(0m, 0, 0, null, null, null, 0, 0), view.Summary);
    }

    // ── Summary and sorting ─────────────────────────────────────────────────

    [Fact]
    public void Summary_TotalsWinsLossesRateAndExtremes()
    {
        var view = View(
        [
            Buy("B1", 100, 10m, 0),
            Sell("S1", 10, 18m, 1),     // +80
            Sell("S2", 10, 7.5m, 2),    // −25
            Sell("S3", 10, 11m, 3),     // +10
            Sell("S4", 10, 10m, 4),     // 0
            Sell("S5", 10, 4m, 5),      // −60
            Sell("X", 999, 10m, 6),     // oversell
            Order("O1", "SELL", 1, 1m, At(7), type: "OPTION")
        ]);

        Assert.Equal(new WebullTradeSummary(5m, 2, 2, 50m, 80m, -60m, 1, 1), view.Summary);
    }

    [Fact]
    public void Summary_WinRateRoundsToTwoDecimals()
    {
        var view = View([Buy("B1", 3, 10m, 0), Sell("S1", 1, 11m, 1), Sell("S2", 1, 12m, 2), Sell("S3", 1, 9m, 3)]);

        Assert.Equal(66.67m, view.Summary.WinRate);
    }

    [Fact]
    public void Summary_NoTrades_ZeroTotalAndNullFigures()
    {
        Assert.Equal(new WebullTradeSummary(0m, 0, 0, null, null, null, 0, 0), View([Buy("B1", 1, 10m, 0)]).Summary);
    }

    [Fact]
    public void Trades_AreNewestFirst_ThenOrdinalOrderId()
    {
        var view = View([Buy("B0", 10, 10m, 0), Sell("S-old", 1, 11m, 1), Sell("b", 1, 11m, 3), Sell("S-new", 1, 11m, 5), Sell("A", 1, 11m, 3)]);

        Assert.Equal(["S-new", "A", "b", "S-old"], view.Trades.Select(t => t.OrderId));
    }
}
