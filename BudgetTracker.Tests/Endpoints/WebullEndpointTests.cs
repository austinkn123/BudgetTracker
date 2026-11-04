using System.Text.Json;
using System.Text.Json.Serialization;
using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Webull;
using BudgetTracker.Server.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BudgetTracker.Tests.Endpoints;

/// <summary>
/// Handler-level tests for <c>/api/webull</c>: status codes, which manager call each route makes, and the exact JSON shape
/// the client is built against.
/// </summary>
public class WebullEndpointTests
{
    private readonly Mock<IWebullManager> _manager = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUserProvider> _currentUser = new();

    private static readonly WebullConnectionView View =
        new(WebullState.Connected, [], null, false, null, null, false);

    public WebullEndpointTests()
    {
        _currentUser.SetupGet(u => u.UserId).Returns(5);
    }

    [Fact]
    public async Task Get_Returns200WithStoredView()
    {
        _manager.Setup(m => m.GetAsync(5)).ReturnsAsync(View);

        var result = await WebullEndpoints.GetAsync(_manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
        Assert.Same(View, Assert.IsType<Ok<WebullConnectionView>>(result).Value);
    }

    [Fact]
    public async Task Refresh_Returns200WithView()
    {
        var failed = View with { LastError = WebullErrorKind.Unreachable };
        _manager.Setup(m => m.RefreshAsync(5)).ReturnsAsync(failed);

        var result = await WebullEndpoints.RefreshAsync(_manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
        Assert.Same(failed, Assert.IsType<Ok<WebullConnectionView>>(result).Value);
    }

    [Fact]
    public async Task Verify_Success_Returns200WithView()
    {
        _manager.Setup(m => m.StartVerificationAsync(5)).ReturnsAsync(Result<WebullConnectionView>.Success(View));

        var result = await WebullEndpoints.VerifyAsync(_manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
        Assert.Same(View, Assert.IsType<Ok<WebullConnectionView>>(result).Value);
    }

    [Fact]
    public async Task Verify_CannotVerify_Returns409WithError()
    {
        _manager.Setup(m => m.StartVerificationAsync(5))
            .ReturnsAsync(Result<WebullConnectionView>.Failure("Webull verification is not available while Connected."));

        var result = await WebullEndpoints.VerifyAsync(_manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status409Conflict, EndpointAssert.StatusOf(result));
        Assert.Equal("Webull verification is not available while Connected.", EndpointAssert.ErrorOf(result));
    }

    /// <summary>Program.cs: Minimal API web defaults plus a global <see cref="JsonStringEnumConverter"/>.</summary>
    private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void View_SerializesToTheShapeTheClientExpects()
    {
        var view = new WebullConnectionView(
            WebullState.WaitingForApproval,
            [new WebullBalance("ACC1", "Individual Cash", "CASH", 1234.5m, null, "USD")],
            new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc),
            true,
            WebullErrorKind.Unreachable,
            new DateTime(2026, 10, 22, 12, 5, 0, DateTimeKind.Utc),
            false);

        Assert.Equal(
            """{"state":"WaitingForApproval","balances":[{"accountId":"ACC1","accountLabel":"Individual Cash","accountType":"CASH","totalNetLiquidationValue":1234.5,"totalCashBalance":null,"currency":"USD"}],"asOf":"2026-10-22T12:00:00Z","isStale":true,"lastError":"Unreachable","approvalExpiresAt":"2026-10-22T12:05:00Z","canVerify":false}""",
            JsonSerializer.Serialize(view, ApiJson));
    }

    [Fact]
    public void EmptyView_SerializesNullsAndEmptyBalances()
    {
        var view = new WebullConnectionView(WebullState.NotSetUp, [], null, false, null, null, false);

        Assert.Equal(
            """{"state":"NotSetUp","balances":[],"asOf":null,"isStale":false,"lastError":null,"approvalExpiresAt":null,"canVerify":false}""",
            JsonSerializer.Serialize(view, ApiJson));
    }

    // ── GET /api/webull/holdings ────────────────────────────────────────────

    [Fact]
    public async Task Holdings_Returns200WithTheSavedHoldingsView()
    {
        var holdings = new WebullHoldingsView(WebullState.Connected, null, false, null, []);
        _manager.Setup(m => m.GetHoldingsAsync(5)).ReturnsAsync(holdings);

        var result = await WebullEndpoints.GetHoldingsAsync(_manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
        Assert.Same(holdings, Assert.IsType<Ok<WebullHoldingsView>>(result).Value);
    }

    [Fact]
    public void HoldingsView_SerializesToTheShapeTheClientExpects()
    {
        var view = new WebullHoldingsView(
            WebullState.Connected,
            new DateTime(2026, 10, 23, 20, 35, 0, DateTimeKind.Utc),
            false,
            null,
            [
                new WebullAccountHoldings("ACC1", "Individual Cash", "CASH", "USD", 5000.5m, 1200m, -12.5m, 302.5m, 25.21m,
                [
                    new WebullHolding("AAPL", "EQUITY", 10m, 120m, 1200m, 1502.5m, 302.5m, 25.21m, 100m),
                    new WebullHolding("SPY260116C00600000", "OPTION", 2m, null, null, null, null, null, null)
                ])
            ]);

        Assert.Equal(
            """{"state":"Connected","asOf":"2026-10-23T20:35:00Z","isStale":false,"lastError":null,"accounts":[{"accountId":"ACC1","accountLabel":"Individual Cash","accountType":"CASH","currency":"USD","totalValue":5000.5,"cash":1200,"dayProfitLoss":-12.5,"openProfitLoss":302.5,"openProfitLossPercent":25.21,"positions":[{"symbol":"AAPL","instrumentType":"EQUITY","quantity":10,"averageCost":120,"totalCost":1200,"marketValue":1502.5,"openProfitLoss":302.5,"openProfitLossPercent":25.21,"percentOfHoldings":100},{"symbol":"SPY260116C00600000","instrumentType":"OPTION","quantity":2,"averageCost":null,"totalCost":null,"marketValue":null,"openProfitLoss":null,"openProfitLossPercent":null,"percentOfHoldings":null}]}]}""",
            JsonSerializer.Serialize(view, ApiJson));
    }

    // ── Trades (Phase 3) ────────────────────────────────────────────────────

    private readonly Mock<IWebullTradesManager> _trades = new(MockBehavior.Strict);

    private static readonly WebullTradeHistoryStatus History =
        new(WebullState.Connected, new DateTime(2026, 10, 25, 14, 55, 0, DateTimeKind.Utc), new DateOnly(2024, 12, 31), false, WebullErrorKind.Unreachable);

    private static readonly WebullTradesView TradesView = new(
        History,
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 10, 25),
        new WebullTradeSummary(167.25m, 1, 0, 100m, 167.25m, null, 2, 1),
        [new WebullClosedTrade("ACC1", "O2", new DateOnly(2026, 6, 1), "BTCUSD", "CRYPTO", 0.015m, 53850m, 65000m, 167.25m, 20.71m)]);

    [Theory]
    [InlineData(null, WebullRange.YearToDate)]
    [InlineData("", WebullRange.YearToDate)]
    [InlineData("1M", WebullRange.OneMonth)]
    [InlineData("1m", WebullRange.OneMonth)]
    [InlineData("3M", WebullRange.ThreeMonths)]
    [InlineData("YTD", WebullRange.YearToDate)]
    [InlineData("ytd", WebullRange.YearToDate)]
    [InlineData("1Y", WebullRange.OneYear)]
    [InlineData("1y", WebullRange.OneYear)]
    [InlineData("All", WebullRange.All)]
    [InlineData("ALL", WebullRange.All)]
    public async Task Trades_KnownRange_Returns200WithTheView(string? range, WebullRange expected)
    {
        _trades.Setup(m => m.GetTradesAsync(5, expected)).ReturnsAsync(TradesView);

        var result = await WebullEndpoints.GetTradesAsync(_trades.Object, _currentUser.Object, range);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
        Assert.Same(TradesView, Assert.IsType<Ok<WebullTradesView>>(result).Value);
    }

    [Theory]
    [InlineData("0")]          // enum binding would accept numbers
    [InlineData("2")]
    [InlineData("YearToDate")] // enum names are not the query values
    [InlineData("OneMonth")]
    [InlineData("1W")]
    [InlineData(" 1M")]
    [InlineData("6M")]
    public async Task Trades_UnknownRange_Returns400WithError_WithoutCallingTheManager(string range)
    {
        var result = await WebullEndpoints.GetTradesAsync(_trades.Object, _currentUser.Object, range);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Equal("Unknown range. Use 1M, 3M, YTD, 1Y or All.", EndpointAssert.ErrorOf(result));
    }

    [Fact]
    public async Task SyncTrades_Returns200WithTheProgress()
    {
        _trades.Setup(m => m.SyncAsync(5)).ReturnsAsync(History);

        var result = await WebullEndpoints.SyncTradesAsync(_trades.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
        Assert.Same(History, Assert.IsType<Ok<WebullTradeHistoryStatus>>(result).Value);
    }

    [Fact]
    public async Task SyncTrades_ManagerThrows_Propagates()
    {
        // Never mapped to a 200: the client stops its chain on a failed request, not on a 200 with lastError null.
        var failure = new DbUpdateException("db down");
        _trades.Setup(m => m.SyncAsync(5)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<DbUpdateException>(() => WebullEndpoints.SyncTradesAsync(_trades.Object, _currentUser.Object)));
    }

    [Fact]
    public void TradesView_SerializesToTheShapeTheClientExpects()
    {
        Assert.Equal(
            """{"history":{"state":"Connected","asOf":"2026-10-25T14:55:00Z","historyFrom":"2024-12-31","isComplete":false,"lastError":"Unreachable"},"from":"2026-01-01","to":"2026-10-25","summary":{"totalRealizedProfitLoss":167.25,"wins":1,"losses":0,"winRate":100,"largestWin":167.25,"largestLoss":null,"excludedIncompleteCost":2,"excludedUnsupported":1},"trades":[{"accountId":"ACC1","orderId":"O2","closeDate":"2026-06-01","symbol":"BTCUSD","instrumentType":"CRYPTO","quantity":0.015,"averageCost":53850,"salePrice":65000,"realizedProfitLoss":167.25,"realizedProfitLossPercent":20.71}]}""",
            JsonSerializer.Serialize(TradesView, ApiJson));
    }

    [Fact]
    public void EmptyTradesView_SerializesNulls()
    {
        var view = new WebullTradesView(
            new WebullTradeHistoryStatus(WebullState.NotSetUp, null, null, true, null),
            null,
            new DateOnly(2026, 10, 25),
            new WebullTradeSummary(0m, 0, 0, null, null, null, 0, 0),
            []);

        Assert.Equal(
            """{"history":{"state":"NotSetUp","asOf":null,"historyFrom":null,"isComplete":true,"lastError":null},"from":null,"to":"2026-10-25","summary":{"totalRealizedProfitLoss":0,"wins":0,"losses":0,"winRate":null,"largestWin":null,"largestLoss":null,"excludedIncompleteCost":0,"excludedUnsupported":0},"trades":[]}""",
            JsonSerializer.Serialize(view, ApiJson));
    }

    [Fact]
    public void TradeHistoryStatus_SerializesToTheShapeTheClientExpects()
    {
        Assert.Equal(
            """{"state":"Connected","asOf":"2026-10-25T14:55:00Z","historyFrom":"2024-12-31","isComplete":false,"lastError":"Unreachable"}""",
            JsonSerializer.Serialize(History, ApiJson));
    }
}
