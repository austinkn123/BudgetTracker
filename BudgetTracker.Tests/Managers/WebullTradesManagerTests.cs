using System.Net;
using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Plaid;
using BudgetTracker.Domain.Webull;
using BudgetTracker.Server.Managers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BudgetTracker.Tests.Managers;

/// <summary>
/// Orchestration tests for <see cref="WebullTradesManager"/> with strict mocks: the per-account plan → fetch → advance →
/// save loop with one <c>now</c>, the 12-page and 15-second time box (on a fake clock, never sleeping), the failure path,
/// no Webull call unless Connected, and no Webull call or Webull table read when not configured. The "never checks or
/// creates a token" check runs the real engines over every state.
/// </summary>
public class WebullTradesManagerTests
{
    private const int UserId = 5;
    private const string Token = "tok-plain";
    private static readonly DateTime Now = new(2026, 10, 25, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IWebullEngine> _engine = new(MockBehavior.Strict);
    private readonly Mock<IWebullOrderSyncEngine> _syncEngine = new(MockBehavior.Strict);
    private readonly Mock<IWebullTradesEngine> _tradesEngine = new(MockBehavior.Strict);
    private readonly Mock<IWebullOrderHistoryAccessor> _history = new(MockBehavior.Strict);
    private readonly Mock<IWebullConnectionAccessor> _connections = new(MockBehavior.Strict);
    private readonly Mock<IWebullOrderAccessor> _orders = new(MockBehavior.Strict);
    private readonly Mock<ILogger<WebullTradesManager>> _logger = new();
    private readonly FakeClock _clock = new(Now);
    private readonly List<string> _log = [];

    /// <summary>A clock that only moves when told to, for both wall time and elapsed time.</summary>
    private sealed class FakeClock(DateTime start) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(start);
        public List<TimeSpan> Delays { get; } = [];
        public override DateTimeOffset GetUtcNow() => UtcNow;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => UtcNow.UtcTicks;
        public void Advance(TimeSpan by) => UtcNow += by;

        /// <summary>A <c>Task.Delay</c> on this clock moves it by the delay and completes at once: nothing sleeps.</summary>
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            Advance(dueTime);
            callback(state);
            return new Timer(_ => { });
        }
    }

    private static readonly WebullSnapshot Snapshot = new(WebullTokenStatus.Normal, Now.AddDays(-1), Now.AddMinutes(-5), null,
        [new WebullBalance("ACC1", "Individual Cash", "CASH", 100m, 25m, "USD"), new WebullBalance("ACC2", null, "MARGIN", 5m, null, "USD")]);

    private static readonly IReadOnlyList<WebullOrderSync> SavedSyncs =
        [new("ACC2", Now.AddYears(-1), Now.AddHours(-1), null, null), new("OLD", null, null, null, WebullErrorKind.KeyRejected)];

    private static readonly IReadOnlyList<WebullOrder> SavedOrders =
    [
        new("ACC1", "A1", "AAPL", "EQUITY", "BUY", "FILLED", 1m, 100m, Now.AddDays(-3), Now.AddDays(-3)),
        new("ACC2", "B1", "BTCUSD", "CRYPTO", "BUY", "PENDING", null, null, Now.AddDays(-2), null)
    ];

    private static readonly WebullTradeHistoryStatus Status = new(WebullState.Connected, Now, new DateOnly(2025, 1, 1), false, null);

    private static WebullConnectionView View(WebullState state) => new(state, [], null, false, null, null, false);

    private static IOptions<WebullOptions> Configured(bool configured = true) =>
        Options.Create(configured ? new WebullOptions { AppKey = "key", AppSecret = "secret" } : new WebullOptions());

    private WebullTradesManager BuildSut(bool configured = true) =>
        new(_engine.Object, _syncEngine.Object, _tradesEngine.Object, _history.Object, _connections.Object, _orders.Object,
            Configured(configured), _logger.Object, _clock);

    private static WebullOrderSync Empty(string accountId) => new(accountId, null, null, null, null);

    private static WebullOrderWindow Window(int n, string? key) => new(Now.AddDays(-n - 1), Now.AddDays(-n), key);

    private static WebullOrderPage Page(string id, string? nextKey, string account = "ACC1") =>
        new([new WebullOrder(account, id, "AAPL", "EQUITY", "SELL", "FILLED", 1m, 110m, Now.AddDays(-1), Now.AddDays(-1))], nextKey);

    /// <summary>Configured and Connected, with ACC1 and ACC2 current, the token readable, and the final status stubbed.</summary>
    private void StubConnected(WebullState state = WebullState.Connected)
    {
        _connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(Snapshot);
        _engine.Setup(e => e.BuildView(true, Snapshot, Now)).Returns(View(state));
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _orders.Setup(o => o.GetSyncsAsync(UserId)).ReturnsAsync(SavedSyncs);
        _orders.Setup(o => o.GetOrdersAsync(UserId)).ReturnsAsync(SavedOrders);
        _syncEngine.Setup(s => s.BuildStatus(state, new[] { "ACC1", "ACC2" }, SavedSyncs)).Returns(Status with { State = state });
    }

    private void StubFetch(string accountId, WebullOrderWindow window, WebullOrderPage page) =>
        _history.Setup(h => h.GetOrderPageAsync(Token, accountId, window, It.IsAny<CancellationToken>()))
            .Callback(() => _log.Add($"fetch {accountId} {window.PaginationKey}"))
            .ReturnsAsync(page);

    /// <summary>Each fetch of <paramref name="window"/> takes the next outcome (a page, or an exception to throw) and logs the clock.</summary>
    private void StubFetches(string accountId, WebullOrderWindow window, params object[] outcomes)
    {
        var queue = new Queue<object>(outcomes);
        _history.Setup(h => h.GetOrderPageAsync(Token, accountId, window, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                _log.Add($"fetch {accountId} {window.PaginationKey} at {(_clock.UtcNow - Now).TotalSeconds}s");
                var next = queue.Dequeue();
                return next is WebullOrderPage page ? Task.FromResult(page) : Task.FromException<WebullOrderPage>((Exception)next);
            });
    }

    private static WebullApiException TooManyRequests() => new(WebullErrorKind.Unreachable, "TOO_MANY_REQUESTS",
        "Webull /trading/orders/historical-orders/list failed with HTTP 429: TOO_MANY_REQUESTS.", HttpStatusCode.TooManyRequests);

    private void StubSave(WebullOrderSync sync, IReadOnlyList<WebullOrder> orders) =>
        _orders.Setup(o => o.SavePageAsync(UserId, sync, orders))
            .Callback(() => _log.Add($"save {sync.AccountId} {sync.Window?.PaginationKey}"))
            .Returns(Task.CompletedTask);

    // ── SyncAsync: the loop ─────────────────────────────────────────────────

    [Fact]
    public async Task Sync_PagesEachCurrentAccountInOrder_UntilTheEngineIsDone_ThenReturnsTheStatus()
    {
        StubConnected();
        IReadOnlyList<WebullOrder> acc1Orders = [SavedOrders[0]];
        IReadOnlyList<WebullOrder> acc2Orders = [SavedOrders[1]];
        var acc2Sync = SavedSyncs[0];

        // ACC1 (no saved sync): two pages of one window.
        var acc1Paging = Empty("ACC1") with { Window = Window(1, "k2") };
        var acc1Done = Empty("ACC1") with { CoveredFrom = Window(1, null).Start, CoveredTo = Window(1, null).End };
        _syncEngine.Setup(s => s.NextWindow(Empty("ACC1"), acc1Orders, Now)).Returns(Window(1, null));
        StubFetch("ACC1", Window(1, null), Page("P1", "k2"));
        _syncEngine.Setup(s => s.Advance(Empty("ACC1"), Window(1, null), "k2")).Returns(acc1Paging);
        StubSave(acc1Paging, Page("P1", "k2").Orders);
        _syncEngine.Setup(s => s.NextWindow(acc1Paging, acc1Orders, Now)).Returns(Window(1, "k2"));
        StubFetch("ACC1", Window(1, "k2"), Page("P2", null));
        _syncEngine.Setup(s => s.Advance(acc1Paging, Window(1, "k2"), null)).Returns(acc1Done);
        StubSave(acc1Done, Page("P2", null).Orders);
        _syncEngine.Setup(s => s.NextWindow(acc1Done, acc1Orders, Now)).Returns((WebullOrderWindow?)null);

        // ACC2 (saved sync): one page.
        var acc2Done = acc2Sync with { CoveredTo = Now };
        _syncEngine.Setup(s => s.NextWindow(acc2Sync, acc2Orders, Now)).Returns(Window(2, null));
        StubFetch("ACC2", Window(2, null), Page("P3", null, "ACC2"));
        _syncEngine.Setup(s => s.Advance(acc2Sync, Window(2, null), null)).Returns(acc2Done);
        StubSave(acc2Done, Page("P3", null, "ACC2").Orders);
        _syncEngine.Setup(s => s.NextWindow(acc2Done, acc2Orders, Now)).Returns((WebullOrderWindow?)null);

        var status = await BuildSut().SyncAsync(UserId);

        Assert.Equal(Status, status);
        Assert.Equal(["fetch ACC1 ", "save ACC1 k2", "fetch ACC1 k2", "save ACC1 ", "fetch ACC2 ", "save ACC2 "], _log);
        _connections.Verify(c => c.SetLastErrorAsync(It.IsAny<int>(), It.IsAny<WebullErrorKind>()), Times.Never);
    }

    /// <summary>Every account always has another page: only the time box ends the loop.</summary>
    private void StubEndlessPages()
    {
        _syncEngine.Setup(s => s.NextWindow(It.IsAny<WebullOrderSync>(), It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns(Window(1, "k"));
        _history.Setup(h => h.GetOrderPageAsync(Token, It.IsAny<string>(), Window(1, "k"), It.IsAny<CancellationToken>()))
            .Callback((string _, string account, WebullOrderWindow _, CancellationToken _) => _log.Add($"fetch {account}"))
            .ReturnsAsync(Page("P", "k"));
        _syncEngine.Setup(s => s.Advance(It.IsAny<WebullOrderSync>(), Window(1, "k"), "k"))
            .Returns((WebullOrderSync sync, WebullOrderWindow window, string _) => sync with { Window = window });
        _orders.Setup(o => o.SavePageAsync(UserId, It.IsAny<WebullOrderSync>(), It.IsAny<IReadOnlyList<WebullOrder>>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Sync_StopsBeforeStartingAThirteenthPage()
    {
        StubConnected();
        StubEndlessPages();

        Assert.Equal(Status, await BuildSut().SyncAsync(UserId));

        Assert.Equal(Enumerable.Repeat("fetch ACC1", 12), _log); // ACC2 waits for the next call
        _orders.Verify(o => o.SavePageAsync(UserId, It.IsAny<WebullOrderSync>(), It.IsAny<IReadOnlyList<WebullOrder>>()), Times.Exactly(12));
    }

    [Fact]
    public async Task Sync_StopsBeforeStartingAPageOnce15SecondsHavePassed_KeepingOneNowThroughout()
    {
        StubConnected();
        StubEndlessPages();
        _orders.Setup(o => o.SavePageAsync(UserId, It.IsAny<WebullOrderSync>(), It.IsAny<IReadOnlyList<WebullOrder>>()))
            .Callback(() => _clock.Advance(TimeSpan.FromSeconds(5)))
            .Returns(Task.CompletedTask);

        Assert.Equal(Status, await BuildSut().SyncAsync(UserId));

        // Pages start at 0 s, 5 s and 10 s; at exactly 15 s the box is spent. NextWindow always got the start time.
        Assert.Equal(Enumerable.Repeat("fetch ACC1", 3), _log);
        _syncEngine.Verify(s => s.NextWindow(It.IsAny<WebullOrderSync>(), It.IsAny<IReadOnlyList<WebullOrder>>(), It.Is<DateTime>(d => d != Now)), Times.Never);
    }

    [Fact]
    public async Task Sync_JustUnder15Seconds_StartsAnotherPage()
    {
        StubConnected();
        StubEndlessPages();
        _orders.Setup(o => o.SavePageAsync(UserId, It.IsAny<WebullOrderSync>(), It.IsAny<IReadOnlyList<WebullOrder>>()))
            .Callback(() => _clock.Advance(TimeSpan.FromSeconds(15) - TimeSpan.FromTicks(1)))
            .Returns(Task.CompletedTask);

        await BuildSut().SyncAsync(UserId);

        Assert.Equal(Enumerable.Repeat("fetch ACC1", 2), _log);
    }

    // ── SyncAsync: failures ─────────────────────────────────────────────────

    [Theory]
    [InlineData(WebullErrorKind.Unreachable, null)] // no response, or an unreadable one
    [InlineData(WebullErrorKind.Unreachable, 500)]
    [InlineData(WebullErrorKind.Unreachable, 503)]
    [InlineData(WebullErrorKind.KeyRejected, 401)]
    [InlineData(WebullErrorKind.KeyRejected, 403)]
    public async Task Sync_WebullFailure_SavesFailedProgressAlone_LogsAndEndsTheCall(WebullErrorKind kind, int? status)
    {
        StubConnected();
        var paging = Empty("ACC1") with { Window = Window(1, "k") };
        var failed = paging with { Window = Window(1, null), LastError = kind };
        var exception = new WebullApiException(kind, null, $"Webull failed with HTTP {status}: no error_code.", (HttpStatusCode?)status);
        _syncEngine.Setup(s => s.NextWindow(Empty("ACC1"), It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns(Window(1, null));
        StubFetch("ACC1", Window(1, null), Page("P1", "k"));
        _syncEngine.Setup(s => s.Advance(Empty("ACC1"), Window(1, null), "k")).Returns(paging);
        StubSave(paging, Page("P1", "k").Orders);
        _syncEngine.Setup(s => s.NextWindow(paging, It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns(Window(1, "k"));
        _history.Setup(h => h.GetOrderPageAsync(Token, "ACC1", Window(1, "k"), It.IsAny<CancellationToken>())).ThrowsAsync(exception);
        _syncEngine.Setup(s => s.Fail(paging, kind)).Returns(failed);
        StubSave(failed, []);

        Assert.Equal(Status, await BuildSut().SyncAsync(UserId));

        Assert.Equal(["fetch ACC1 ", "save ACC1 k", "save ACC1 "], _log); // no retry; ACC2 waits
        Assert.Empty(_clock.Delays);
        _orders.Verify(o => o.SavePageAsync(UserId, failed, It.Is<IReadOnlyList<WebullOrder>>(l => l.Count == 0)), Times.Once);
        _connections.Verify(c => c.SetLastErrorAsync(It.IsAny<int>(), It.IsAny<WebullErrorKind>()), Times.Never);
        _logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), exception,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Sync_TokenRejected_AlsoRecordedOnTheConnection()
    {
        StubConnected();
        var failed = Empty("ACC1") with { LastError = WebullErrorKind.TokenRejected };
        _syncEngine.Setup(s => s.NextWindow(Empty("ACC1"), It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns(Window(1, null));
        _history.Setup(h => h.GetOrderPageAsync(Token, "ACC1", Window(1, null), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WebullApiException(WebullErrorKind.TokenRejected, "INVALID_TOKEN", "Webull failed with HTTP 417: INVALID_TOKEN.",
                HttpStatusCode.ExpectationFailed));
        _syncEngine.Setup(s => s.Fail(Empty("ACC1"), WebullErrorKind.TokenRejected)).Returns(failed);
        StubSave(failed, []);
        _connections.Setup(c => c.SetLastErrorAsync(UserId, WebullErrorKind.TokenRejected)).Returns(Task.CompletedTask);

        Assert.Equal(Status, await BuildSut().SyncAsync(UserId));

        _connections.Verify(c => c.SetLastErrorAsync(UserId, WebullErrorKind.TokenRejected), Times.Once);
        Assert.Empty(_clock.Delays);
    }

    // ── SyncAsync: HTTP 429 ─────────────────────────────────────────────────

    [Fact]
    public async Task Sync_A429ThenA200_RetriesTheSameWindowAndKeyAfterABackoff_SavesThePage_RecordsNoError()
    {
        StubConnected();
        var done = Empty("ACC1") with { CoveredFrom = Window(1, null).Start, CoveredTo = Window(1, null).End };
        _syncEngine.Setup(s => s.NextWindow(Empty("ACC1"), It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns(Window(1, "k"));
        StubFetches("ACC1", Window(1, "k"), TooManyRequests(), Page("P1", null));
        _syncEngine.Setup(s => s.Advance(Empty("ACC1"), Window(1, "k"), null)).Returns(done);
        StubSave(done, Page("P1", null).Orders);
        _syncEngine.Setup(s => s.NextWindow(done, It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns((WebullOrderWindow?)null);
        _syncEngine.Setup(s => s.NextWindow(SavedSyncs[0], It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns((WebullOrderWindow?)null);

        Assert.Equal(Status, await BuildSut().SyncAsync(UserId));

        Assert.Equal(["fetch ACC1 k at 0s", "fetch ACC1 k at 2.5s", "save ACC1 "], _log);
        Assert.Equal([TimeSpan.FromSeconds(2.5)], _clock.Delays);
        _syncEngine.Verify(s => s.Fail(It.IsAny<WebullOrderSync>(), It.IsAny<WebullErrorKind>()), Times.Never);
        _connections.Verify(c => c.SetLastErrorAsync(It.IsAny<int>(), It.IsAny<WebullErrorKind>()), Times.Never);
        // Information or Warning, with the attempt number, and never the token.
        _logger.Verify(l => l.Log(It.Is<LogLevel>(level => level == LogLevel.Information || level == LogLevel.Warning), It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("429") && v.ToString()!.Contains("retry 1 of 2") && !v.ToString()!.Contains(Token)),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Sync_ThreeConsecutive429s_RecordsUnreachableOnce_AndFetchesNothingMoreThatCall()
    {
        StubConnected();
        var failed = Empty("ACC1") with { LastError = WebullErrorKind.Unreachable };
        _syncEngine.Setup(s => s.NextWindow(Empty("ACC1"), It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns(Window(1, "k"));
        StubFetches("ACC1", Window(1, "k"), TooManyRequests(), TooManyRequests(), TooManyRequests()); // a fourth fetch would throw
        _syncEngine.Setup(s => s.Fail(Empty("ACC1"), WebullErrorKind.Unreachable)).Returns(failed);
        StubSave(failed, []);

        Assert.Equal(Status, await BuildSut().SyncAsync(UserId));

        Assert.Equal(["fetch ACC1 k at 0s", "fetch ACC1 k at 2.5s", "fetch ACC1 k at 5s", "save ACC1 "], _log); // ACC2 waits
        Assert.Equal([TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(2.5)], _clock.Delays);
        _syncEngine.Verify(s => s.Fail(It.IsAny<WebullOrderSync>(), It.IsAny<WebullErrorKind>()), Times.Once);
        _orders.Verify(o => o.SavePageAsync(UserId, failed, It.Is<IReadOnlyList<WebullOrder>>(l => l.Count == 0)), Times.Once);
        _connections.Verify(c => c.SetLastErrorAsync(It.IsAny<int>(), It.IsAny<WebullErrorKind>()), Times.Never);
    }

    [Fact]
    public async Task Sync_TimeBoxRunsOutDuringA429Backoff_ReturnsTheStatusWithNoError_KeepingProgress()
    {
        StubConnected();
        var paging = Empty("ACC1") with { Window = Window(1, "k") };
        _syncEngine.Setup(s => s.NextWindow(Empty("ACC1"), It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns(Window(1, null));
        StubFetches("ACC1", Window(1, null), Page("P1", "k"));
        _syncEngine.Setup(s => s.Advance(Empty("ACC1"), Window(1, null), "k")).Returns(paging);
        var p1Orders = Page("P1", "k").Orders;
        _orders.Setup(o => o.SavePageAsync(UserId, paging, p1Orders))
            .Callback(() => { _log.Add("save ACC1 k"); _clock.Advance(TimeSpan.FromSeconds(13)); })
            .Returns(Task.CompletedTask);
        _syncEngine.Setup(s => s.NextWindow(paging, It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns(Window(1, "k"));
        StubFetches("ACC1", Window(1, "k"), TooManyRequests()); // a retry would throw: 13 s + 2.5 s is past the box

        var status = await BuildSut().SyncAsync(UserId);

        Assert.Equal(Status, status);
        Assert.Null(status.LastError);
        Assert.Equal(["fetch ACC1  at 0s", "save ACC1 k", "fetch ACC1 k at 13s"], _log);
        _syncEngine.Verify(s => s.Fail(It.IsAny<WebullOrderSync>(), It.IsAny<WebullErrorKind>()), Times.Never);
        _orders.Verify(o => o.SavePageAsync(UserId, It.IsAny<WebullOrderSync>(), It.IsAny<IReadOnlyList<WebullOrder>>()), Times.Once);
    }

    [Fact]
    public async Task Sync_ASuccessfulPageResetsTheConsecutive429Count_RecordsNoError()
    {
        // Two 429s, a page, two 429s, a page: four 429s in all but never three in a row.
        StubConnected();
        var paging = Empty("ACC1") with { Window = Window(1, "k") };
        var done = Empty("ACC1") with { CoveredFrom = Window(1, null).Start, CoveredTo = Window(1, null).End };
        _syncEngine.Setup(s => s.NextWindow(Empty("ACC1"), It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns(Window(1, null));
        StubFetches("ACC1", Window(1, null), TooManyRequests(), TooManyRequests(), Page("P1", "k"));
        _syncEngine.Setup(s => s.Advance(Empty("ACC1"), Window(1, null), "k")).Returns(paging);
        StubSave(paging, Page("P1", "k").Orders);
        _syncEngine.Setup(s => s.NextWindow(paging, It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns(Window(1, "k"));
        StubFetches("ACC1", Window(1, "k"), TooManyRequests(), TooManyRequests(), Page("P2", null));
        _syncEngine.Setup(s => s.Advance(paging, Window(1, "k"), null)).Returns(done);
        StubSave(done, Page("P2", null).Orders);
        _syncEngine.Setup(s => s.NextWindow(done, It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns((WebullOrderWindow?)null);
        _syncEngine.Setup(s => s.NextWindow(SavedSyncs[0], It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns((WebullOrderWindow?)null);

        Assert.Equal(Status, await BuildSut().SyncAsync(UserId));

        Assert.Equal(
            ["fetch ACC1  at 0s", "fetch ACC1  at 2.5s", "fetch ACC1  at 5s", "save ACC1 k",
             "fetch ACC1 k at 5s", "fetch ACC1 k at 7.5s", "fetch ACC1 k at 10s", "save ACC1 "],
            _log);
        _syncEngine.Verify(s => s.Fail(It.IsAny<WebullOrderSync>(), It.IsAny<WebullErrorKind>()), Times.Never);
    }

    [Fact]
    public async Task Sync_AfterAStoredUnreachable_TheNextSuccessfulPageClearsIt()
    {
        // The real sync engine and a stateful sync table: the stuck row from production (2024 window, key dropped by Fail).
        var stuck = new WebullOrderSync("ACC1", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), Now.AddMinutes(-1),
            new WebullOrderWindow(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc), null),
            WebullErrorKind.Unreachable);
        var snapshot = Snapshot with { Balances = [Snapshot.Balances[0]] };
        var table = new List<WebullOrderSync> { stuck };
        var saved = new List<WebullOrderSync>();
        _connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(snapshot);
        _engine.Setup(e => e.BuildView(true, snapshot, Now)).Returns(View(WebullState.Connected));
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _orders.Setup(o => o.GetSyncsAsync(UserId)).ReturnsAsync(() => table.ToList());
        _orders.Setup(o => o.GetOrdersAsync(UserId)).ReturnsAsync([]);
        _orders.Setup(o => o.SavePageAsync(UserId, It.IsAny<WebullOrderSync>(), It.IsAny<IReadOnlyList<WebullOrder>>()))
            .Callback((int _, WebullOrderSync sync, IReadOnlyList<WebullOrder> _) => { saved.Add(sync); table[0] = sync; })
            .Returns(Task.CompletedTask);
        _history.Setup(h => h.GetOrderPageAsync(Token, "ACC1", It.IsAny<WebullOrderWindow>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebullOrderPage([], null));

        var status = await new WebullTradesManager(_engine.Object, new WebullOrderSyncEngine(), _tradesEngine.Object, _history.Object,
            _connections.Object, _orders.Object, Configured(), NullLogger<WebullTradesManager>.Instance, _clock).SyncAsync(UserId);

        _history.Verify(h => h.GetOrderPageAsync(Token, "ACC1", stuck.Window!, It.IsAny<CancellationToken>()), Times.Once); // resumed there
        Assert.Equal(stuck with { CoveredFrom = stuck.Window!.Start, Window = null, LastError = null }, saved[0]);
        Assert.Null(status.LastError); // what the client's chain checks
    }

    [Fact]
    public async Task Sync_SaveFails_ThrowsWithoutRecordingAWebullFailure()
    {
        // A DB outage must not become a 200 with lastError null: the client would keep chaining Webull calls.
        StubConnected();
        var done = Empty("ACC1") with { CoveredFrom = Window(1, null).Start, CoveredTo = Window(1, null).End };
        _syncEngine.Setup(s => s.NextWindow(Empty("ACC1"), It.IsAny<IReadOnlyList<WebullOrder>>(), Now)).Returns(Window(1, null));
        StubFetch("ACC1", Window(1, null), Page("P1", null));
        _syncEngine.Setup(s => s.Advance(Empty("ACC1"), Window(1, null), null)).Returns(done);
        _orders.Setup(o => o.SavePageAsync(UserId, done, It.IsAny<IReadOnlyList<WebullOrder>>())).ThrowsAsync(new DbUpdateException("db down"));

        await Assert.ThrowsAsync<DbUpdateException>(() => BuildSut().SyncAsync(UserId));

        _syncEngine.Verify(s => s.Fail(It.IsAny<WebullOrderSync>(), It.IsAny<WebullErrorKind>()), Times.Never);
        _connections.Verify(c => c.SetLastErrorAsync(It.IsAny<int>(), It.IsAny<WebullErrorKind>()), Times.Never);
        _history.Verify(h => h.GetOrderPageAsync(Token, It.IsAny<string>(), It.IsAny<WebullOrderWindow>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(AccessTokenStatus.NotFound)]
    [InlineData(AccessTokenStatus.Undecryptable)]
    public async Task Sync_MissingOrUnreadableToken_RecordsTokenRejected_WithoutCallingWebull(AccessTokenStatus lookupStatus)
    {
        StubConnected();
        var lookup = lookupStatus == AccessTokenStatus.NotFound ? AccessTokenLookup.NotFound : AccessTokenLookup.Undecryptable;
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(lookup);
        _connections.Setup(c => c.SetLastErrorAsync(UserId, WebullErrorKind.TokenRejected)).Returns(Task.CompletedTask);

        Assert.Equal(Status, await BuildSut().SyncAsync(UserId));

        _connections.Verify(c => c.SetLastErrorAsync(UserId, WebullErrorKind.TokenRejected), Times.Once);
    }

    // ── SyncAsync: when nothing is fetched ──────────────────────────────────

    [Theory]
    [InlineData(WebullState.NotConnected)]
    [InlineData(WebullState.WaitingForApproval)]
    [InlineData(WebullState.ApprovalExpired)]
    [InlineData(WebullState.NeedsVerification)]
    public async Task Sync_NotConnected_ReturnsSavedProgress_WithoutReadingTheTokenOrCallingWebull(WebullState state)
    {
        _connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(Snapshot);
        _engine.Setup(e => e.BuildView(true, Snapshot, Now)).Returns(View(state));
        _orders.Setup(o => o.GetSyncsAsync(UserId)).ReturnsAsync(SavedSyncs);
        _syncEngine.Setup(s => s.BuildStatus(state, new[] { "ACC1", "ACC2" }, SavedSyncs)).Returns(Status with { State = state });

        Assert.Equal(Status with { State = state }, await BuildSut().SyncAsync(UserId));
    }

    [Fact]
    public async Task Sync_NotConfigured_NotSetUp_WithoutReadingAnything()
    {
        var notSetUp = new WebullTradeHistoryStatus(WebullState.NotSetUp, null, null, true, null);
        _engine.Setup(e => e.BuildView(false, null, Now)).Returns(View(WebullState.NotSetUp));
        _syncEngine.Setup(s => s.BuildStatus(WebullState.NotSetUp, Array.Empty<string>(), Array.Empty<WebullOrderSync>())).Returns(notSetUp);

        Assert.Equal(notSetUp, await BuildSut(configured: false).SyncAsync(UserId));
    }

    [Fact]
    public void Manager_HasNoWayToCheckOrCreateATokenOrTouchBudgetData()
    {
        var dependencies = typeof(WebullTradesManager).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToList();

        Assert.DoesNotContain(typeof(IWebullAccessor), dependencies);
        Assert.DoesNotContain(dependencies, t => t.Name is "ITransactionAccessor" or "IAccountAccessor" or "IBudgetPlanAccessor" or "IPlaidItemAccessor");
    }

    [Fact]
    public async Task Sync_NeverChecksOrCreatesAToken_InAnyState()
    {
        var now = Now;
        WebullTokenStatus?[] statuses = [null, .. Enum.GetValues<WebullTokenStatus>()];
        WebullErrorKind?[] errors = [null, .. Enum.GetValues<WebullErrorKind>()];
        DateTime[] createdAts = [now, now.AddMinutes(-10), now.AddDays(-30)];
        List<WebullSnapshot?> snapshots = [null];
        foreach (var status in statuses)
        foreach (var error in errors)
        foreach (var createdAt in createdAts)
            snapshots.Add(new WebullSnapshot(status, status is null ? null : createdAt, now.AddHours(-1), error, Snapshot.Balances));

        var history = new Mock<IWebullOrderHistoryAccessor>(MockBehavior.Strict);
        history.Setup(h => h.GetOrderPageAsync(Token, It.IsAny<string>(), It.IsAny<WebullOrderWindow>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebullOrderPage([], null));
        var connectedRuns = 0;
        foreach (var snapshot in snapshots)
        foreach (var configured in new[] { true, false })
        {
            var connections = new Mock<IWebullConnectionAccessor>(MockBehavior.Strict);
            connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(snapshot);
            connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
            connections.Setup(c => c.SetLastErrorAsync(UserId, It.IsAny<WebullErrorKind>())).Returns(Task.CompletedTask);
            var orders = new Mock<IWebullOrderAccessor>(MockBehavior.Strict);
            orders.Setup(o => o.GetSyncsAsync(UserId)).ReturnsAsync([]);
            orders.Setup(o => o.GetOrdersAsync(UserId)).ReturnsAsync([]);
            orders.Setup(o => o.SavePageAsync(UserId, It.IsAny<WebullOrderSync>(), It.IsAny<IReadOnlyList<WebullOrder>>())).Returns(Task.CompletedTask);

            var engine = new WebullEngine();
            var status = await new WebullTradesManager(engine, new WebullOrderSyncEngine(), new WebullTradesEngine(), history.Object,
                connections.Object, orders.Object, Configured(configured), NullLogger<WebullTradesManager>.Instance, new FakeClock(now)).SyncAsync(UserId);

            // Strict: SaveTokenAsync and SetTokenStatusAsync were never set up, so any call would have thrown.
            var connected = configured && engine.BuildView(true, snapshot, now).State == WebullState.Connected;
            orders.Verify(o => o.SavePageAsync(UserId, It.IsAny<WebullOrderSync>(), It.IsAny<IReadOnlyList<WebullOrder>>()),
                connected ? Times.AtLeastOnce() : Times.Never());
            if (!configured)
                Assert.Equal(WebullState.NotSetUp, status.State);
            connectedRuns += connected ? 1 : 0;
        }

        // The loop really exercised the Webull path, so "never checked or created" is not vacuous.
        Assert.True(connectedRuns > 0);
        history.Verify(h => h.GetOrderPageAsync(Token, It.IsAny<string>(), It.IsAny<WebullOrderWindow>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public void Manager_ResolvesFromAValidatedContainer_WithTheOrderHistoryTypedClient()
    {
        // Mirrors Program.cs: the typed client, the scanned engines, and no TimeProvider registration (the default is used).
        // Development validates the container at startup, so a missing dependency would stop the dev server.
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<WebullOptions>(_ => { });
        services.AddHttpClient<IWebullOrderHistoryAccessor, WebullAccessor>(client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<IWebullEngine, WebullEngine>();
        services.AddScoped<IWebullOrderSyncEngine, WebullOrderSyncEngine>();
        services.AddScoped<IWebullTradesEngine, WebullTradesEngine>();
        services.AddScoped(_ => Mock.Of<IWebullConnectionAccessor>());
        services.AddScoped(_ => Mock.Of<IWebullOrderAccessor>());
        services.AddScoped<IWebullTradesManager, WebullTradesManager>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<WebullTradesManager>(scope.ServiceProvider.GetRequiredService<IWebullTradesManager>());
    }

    // ── GetTradesAsync ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(WebullRange.YearToDate)]
    [InlineData(WebullRange.All)]
    public async Task GetTrades_BuildsTheViewFromSavedProgressAndOrders_WithoutCallingWebull(WebullRange range)
    {
        StubConnected();
        var view = new WebullTradesView(Status, null, new DateOnly(2026, 10, 25), new WebullTradeSummary(0m, 0, 0, null, null, null, 0, 0), []);
        _tradesEngine.Setup(t => t.BuildView(Status, SavedOrders, range, Now)).Returns(view);

        Assert.Same(view, await BuildSut().GetTradesAsync(UserId, range));

        _connections.Verify(c => c.GetAccessTokenAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetTrades_NotConfigured_NotSetUpWithNoTrades_WithoutReadingAnything()
    {
        var notSetUp = new WebullTradeHistoryStatus(WebullState.NotSetUp, null, null, true, null);
        var view = new WebullTradesView(notSetUp, new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 25), new WebullTradeSummary(0m, 0, 0, null, null, null, 0, 0), []);
        _engine.Setup(e => e.BuildView(false, null, Now)).Returns(View(WebullState.NotSetUp));
        _syncEngine.Setup(s => s.BuildStatus(WebullState.NotSetUp, Array.Empty<string>(), Array.Empty<WebullOrderSync>())).Returns(notSetUp);
        _tradesEngine.Setup(t => t.BuildView(notSetUp, Array.Empty<WebullOrder>(), WebullRange.YearToDate, Now)).Returns(view);

        Assert.Same(view, await BuildSut(configured: false).GetTradesAsync(UserId, WebullRange.YearToDate));
    }
}
