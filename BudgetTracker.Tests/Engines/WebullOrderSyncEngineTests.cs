using System.Globalization;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Tests.Engines;

/// <summary>
/// Tests for <see cref="WebullOrderSyncEngine"/>: which order-history window comes next (in-progress, top, incremental,
/// backfill, none), with the year edges, Webull's 2018-05-21 floor, the 15-minute and 90-day cutoffs and the 7-day overlap
/// checked on both sides; how a page or a failure moves progress; and the status across current accounts.
/// </summary>
public class WebullOrderSyncEngineTests
{
    private static readonly DateTime Floor = new(2018, 5, 21, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Utc("2026-10-25T15:00:00Z");
    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);
    private static readonly TimeSpan Ms = TimeSpan.FromMilliseconds(1);

    private readonly WebullOrderSyncEngine _sut = new();

    private static DateTime Utc(string iso) =>
        DateTime.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

    private static WebullOrderSync Sync(
        DateTime? coveredFrom = null,
        DateTime? coveredTo = null,
        WebullOrderWindow? window = null,
        WebullErrorKind? lastError = null,
        string accountId = "ACC1") =>
        new(accountId, coveredFrom, coveredTo, window, lastError);

    private static WebullOrder Order(DateTime placedAt, string status = "FILLED", string id = "O1") =>
        new("ACC1", id, "AAPL", "EQUITY", "BUY", status, 1m, 100m, placedAt, placedAt);

    /// <summary>Covered from January 1 to an hour ago: due for an incremental pass.</summary>
    private static WebullOrderSync DueForIncremental() =>
        Sync(Utc("2026-01-01T00:00:00Z"), Now.AddHours(-1));

    // ── NextWindow: 1. in progress ──────────────────────────────────────────

    [Fact]
    public void NextWindow_WindowInProgress_ContinuesItWithItsKey()
    {
        var inProgress = new WebullOrderWindow(Utc("2024-01-01T00:00:00Z"), Utc("2024-12-31T23:59:59.999Z"), "pk=2");

        // Even when an incremental pass is due and old orders are open, the window in progress wins.
        var next = _sut.NextWindow(DueForIncremental() with { Window = inProgress }, [Order(Now.AddDays(-30), "PENDING")], Now);

        Assert.Equal(inProgress, next);
    }

    [Fact]
    public void NextWindow_WindowInProgressAfterAFailure_RestartsItFromTheFirstPage()
    {
        var restarted = new WebullOrderWindow(Utc("2026-01-01T00:00:00Z"), Now.AddDays(-1), null);

        Assert.Equal(restarted, _sut.NextWindow(Sync(window: restarted), [], Now));
    }

    // ── NextWindow: 2. top window ───────────────────────────────────────────

    [Theory]
    [InlineData("2026-10-25T15:00:00Z", "2026-01-01T00:00:00Z")]
    [InlineData("2026-01-01T00:00:00Z", "2026-01-01T00:00:00Z")] // the first instant of the year: an empty window
    [InlineData("2025-12-31T23:59:59.999Z", "2025-01-01T00:00:00Z")]
    [InlineData("2027-01-01T03:00:00Z", "2027-01-01T00:00:00Z")] // still Dec 31 in Eastern time; the year is UTC's
    [InlineData("2018-10-01T00:00:00Z", "2018-05-21T00:00:00Z")] // the floor's year starts at the floor
    public void NextWindow_NothingCovered_TopWindowFromJanuaryFirstUtcToNow(string now, string expectedStart)
    {
        var next = _sut.NextWindow(Sync(), [], Utc(now));

        Assert.Equal(new WebullOrderWindow(Utc(expectedStart), Utc(now), null), next);
    }

    [Fact]
    public void NextWindow_NothingCovered_IgnoresSavedOrders()
    {
        var next = _sut.NextWindow(Sync(), [Order(Now.AddDays(-30), "PENDING")], Now);

        Assert.Equal(new WebullOrderWindow(Utc("2026-01-01T00:00:00Z"), Now, null), next);
    }

    // ── NextWindow: 3. incremental ──────────────────────────────────────────

    [Fact]
    public void NextWindow_CoveredToExactly15MinutesAgo_Incremental()
    {
        var coveredTo = Now.AddMinutes(-15);

        var next = _sut.NextWindow(Sync(Floor, coveredTo), [], Now);

        Assert.Equal(new WebullOrderWindow(coveredTo.AddDays(-7), Now, null), next);
    }

    [Fact]
    public void NextWindow_CoveredToJustUnder15MinutesAgo_NoIncremental()
    {
        // Complete back to the floor, so nothing else is due either.
        Assert.Null(_sut.NextWindow(Sync(Floor, Now.AddMinutes(-15) + Tick), [], Now));
    }

    [Fact]
    public void NextWindow_CoveredToJustUnder15MinutesAgo_BackfillsInstead()
    {
        var next = _sut.NextWindow(Sync(Utc("2026-01-01T00:00:00Z"), Now.AddMinutes(-15) + Tick), [], Now);

        Assert.Equal(new WebullOrderWindow(Utc("2025-01-01T00:00:00Z"), Utc("2025-12-31T23:59:59.999Z"), null), next);
    }

    [Fact]
    public void NextWindow_Incremental_StartsSevenDaysBeforeTheNewestSavedOrder()
    {
        IReadOnlyList<WebullOrder> orders =
        [
            Order(Utc("2026-10-01T14:00:00Z"), id: "O1"),
            Order(Utc("2026-10-20T14:30:00.123Z"), "CANCELLED", "O2"), // newest, whatever its status
            Order(Utc("2026-09-01T14:00:00Z"), id: "O3")
        ];

        var next = _sut.NextWindow(DueForIncremental(), orders, Now);

        Assert.Equal(new WebullOrderWindow(Utc("2026-10-13T14:30:00.123Z"), Now, null), next);
    }

    [Fact]
    public void NextWindow_IncrementalWithNoSavedOrders_StartsSevenDaysBeforeCoveredTo()
    {
        var sync = DueForIncremental();

        Assert.Equal(new WebullOrderWindow(sync.CoveredTo!.Value.AddDays(-7), Now, null), _sut.NextWindow(sync, [], Now));
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("SUBMITTED")]
    [InlineData("PARTIAL_FILLED")]
    public void NextWindow_Incremental_ReachesBackToTheOldestOpenOrder(string openStatus)
    {
        var open = Order(Now.AddDays(-60), openStatus, "OPEN");

        var next = _sut.NextWindow(DueForIncremental(), [Order(Now.AddDays(-2), id: "NEW"), open], Now);

        Assert.Equal(new WebullOrderWindow(open.PlacedAt, Now, null), next);
    }

    [Fact]
    public void NextWindow_Incremental_OldestOfSeveralOpenOrdersWins()
    {
        IReadOnlyList<WebullOrder> orders =
        [
            Order(Now.AddDays(-2), id: "NEW"),
            Order(Now.AddDays(-40), "PENDING", "P1"),
            Order(Now.AddDays(-70), "SUBMITTED", "P2"),
            Order(Now.AddDays(-50), "PARTIAL_FILLED", "P3")
        ];

        Assert.Equal(Now.AddDays(-70), _sut.NextWindow(DueForIncremental(), orders, Now)!.Start);
    }

    [Theory]
    [InlineData("FILLED")]
    [InlineData("CANCELLED")]
    [InlineData("FAILED")]
    [InlineData("pending")] // statuses are matched exactly as Webull sends them
    public void NextWindow_Incremental_ClosedOrdersDoNotReachBack(string status)
    {
        var newest = Order(Now.AddDays(-2), id: "NEW");

        var next = _sut.NextWindow(DueForIncremental(), [newest, Order(Now.AddDays(-60), status, "OLD")], Now);

        Assert.Equal(newest.PlacedAt.AddDays(-7), next!.Start);
    }

    [Fact]
    public void NextWindow_Incremental_OpenOrderExactly90DaysOld_StillReachesBack()
    {
        var open = Order(Now.AddDays(-90), "PENDING", "OPEN");

        Assert.Equal(open.PlacedAt, _sut.NextWindow(DueForIncremental(), [Order(Now.AddDays(-2), id: "NEW"), open], Now)!.Start);
    }

    [Fact]
    public void NextWindow_Incremental_OpenOrderJustOver90DaysOld_IsIgnored()
    {
        var newest = Order(Now.AddDays(-2), id: "NEW");
        var stale = Order(Now.AddDays(-90) - Tick, "PENDING", "STALE");

        Assert.Equal(newest.PlacedAt.AddDays(-7), _sut.NextWindow(DueForIncremental(), [newest, stale], Now)!.Start);
    }

    [Fact]
    public void NextWindow_Incremental_OpenOrderInsideTheOverlap_DoesNotMoveTheStart()
    {
        var newest = Order(Now.AddDays(-2), id: "NEW");

        var next = _sut.NextWindow(DueForIncremental(), [newest, Order(Now.AddDays(-5), "PENDING", "OPEN")], Now);

        Assert.Equal(newest.PlacedAt.AddDays(-7), next!.Start);
    }

    [Fact]
    public void NextWindow_Incremental_MayCrossAYear()
    {
        var now = Utc("2026-01-05T12:00:00Z");
        var sync = Sync(Utc("2026-01-01T00:00:00Z"), Utc("2026-01-03T12:00:00Z"));

        var next = _sut.NextWindow(sync, [Order(Utc("2026-01-02T15:00:00Z"))], now);

        Assert.Equal(new WebullOrderWindow(Utc("2025-12-26T15:00:00Z"), now, null), next);
    }

    [Fact]
    public void NextWindow_Incremental_NeverStartsBeforeTheFloor()
    {
        var sync = Sync(Floor, Floor.AddDays(3));

        Assert.Equal(new WebullOrderWindow(Floor, Now, null), _sut.NextWindow(sync, [], Now));
    }

    [Fact]
    public void NextWindow_Incremental_ComesBeforeBackfill()
    {
        // History is far from complete, but fresh orders come first.
        var next = _sut.NextWindow(Sync(Utc("2026-01-01T00:00:00Z"), Now.AddMinutes(-20)), [], Now);

        Assert.Equal(Now, next!.End);
    }

    // ── NextWindow: 4. backfill ─────────────────────────────────────────────

    [Theory]
    [InlineData("2026-01-01T00:00:00Z", "2025-01-01T00:00:00Z", "2025-12-31T23:59:59.999Z")] // the previous year, whole
    [InlineData("2025-12-26T15:00:00Z", "2025-01-01T00:00:00Z", "2025-12-26T14:59:59.999Z")] // the rest of a part year
    [InlineData("2026-01-01T00:00:00.001Z", "2026-01-01T00:00:00Z", "2026-01-01T00:00:00Z")] // 1 ms left in the year
    [InlineData("2019-01-01T00:00:00Z", "2018-05-21T00:00:00Z", "2018-12-31T23:59:59.999Z")] // the floor's year
    [InlineData("2018-09-01T00:00:00Z", "2018-05-21T00:00:00Z", "2018-08-31T23:59:59.999Z")]
    [InlineData("2018-05-21T00:00:00.001Z", "2018-05-21T00:00:00Z", "2018-05-21T00:00:00Z")] // the floor itself is left
    public void NextWindow_CoveredAndFresh_BackfillsTheRestOfTheYearBeforeCoveredFrom(string coveredFrom, string start, string end)
    {
        var next = _sut.NextWindow(Sync(Utc(coveredFrom), Now.AddMinutes(-1)), [], Now);

        Assert.Equal(new WebullOrderWindow(Utc(start), Utc(end), null), next);
    }

    [Fact]
    public void NextWindow_Backfill_SubMillisecondCoveredFrom_EndsAtTheLastWholeMillisecondBeforeIt()
    {
        // Webull's times are whole milliseconds: an order at .123 is not covered by a span starting at .1234567.
        var coveredFrom = Utc("2025-06-01T12:00:00.123Z").AddTicks(4567);

        var next = _sut.NextWindow(Sync(coveredFrom, Now.AddMinutes(-1)), [], Now);

        Assert.Equal(Utc("2025-06-01T12:00:00.123Z"), next!.End);
    }

    [Theory]
    [InlineData("2018-05-21T00:00:00Z")]
    [InlineData("2018-01-01T00:00:00Z")]
    public void NextWindow_CompleteToTheFloorAndFresh_Null(string coveredFrom) =>
        Assert.Null(_sut.NextWindow(Sync(Utc(coveredFrom), Now.AddMinutes(-1)), [], Now));

    [Fact]
    public void NextWindow_FullWalk_WindowsNeverCrossAUtcYearAndStopAtTheFloor()
    {
        var sync = Sync();
        var windows = new List<WebullOrderWindow>();
        while (_sut.NextWindow(sync, [], Now) is { } window && windows.Count < 20)
        {
            windows.Add(window);
            sync = _sut.Advance(sync, window, null);
        }

        Assert.Equal(9, windows.Count); // 2026 (top), then 2025 … 2018
        Assert.All(windows, w => Assert.Equal(w.Start.Year, w.End.Year));
        Assert.All(windows.Zip(windows.Skip(1)), pair => Assert.Equal(pair.First.Start - Ms, pair.Second.End));
        Assert.Equal(Floor, windows[^1].Start);
        Assert.Equal((Floor, Now), (sync.CoveredFrom, sync.CoveredTo));
    }

    // ── Advance ─────────────────────────────────────────────────────────────

    private static readonly WebullOrderWindow Backfill2025 =
        new(Utc("2025-01-01T00:00:00Z"), Utc("2025-12-31T23:59:59.999Z"), null);

    [Fact]
    public void Advance_WithNextKey_KeepsTheWindowInProgressAndCoverageUnchanged()
    {
        var sync = Sync(Utc("2026-01-01T00:00:00Z"), Now.AddMinutes(-1), lastError: WebullErrorKind.Unreachable);

        var advanced = _sut.Advance(sync, Backfill2025 with { PaginationKey = "pk=1" }, "pk=2");

        Assert.Equal(sync with { Window = Backfill2025 with { PaginationKey = "pk=2" }, LastError = null }, advanced);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Advance_WithoutNextKey_CompletesTheWindowAndExtendsCoverageBackward(string? nextKey)
    {
        var sync = Sync(Utc("2026-01-01T00:00:00Z"), Now.AddMinutes(-1), Backfill2025 with { PaginationKey = "pk=1" },
            WebullErrorKind.TokenRejected);

        var advanced = _sut.Advance(sync, Backfill2025 with { PaginationKey = "pk=1" }, nextKey);

        Assert.Equal(Sync(Backfill2025.Start, Now.AddMinutes(-1)), advanced);
    }

    [Fact]
    public void Advance_FirstWindowComplete_CoversExactlyThatWindow()
    {
        var top = new WebullOrderWindow(Utc("2026-01-01T00:00:00Z"), Now, null);

        Assert.Equal(Sync(top.Start, top.End), _sut.Advance(Sync(), top, null));
    }

    [Fact]
    public void Advance_IncrementalComplete_ExtendsCoveredToAndKeepsCoveredFrom()
    {
        var sync = Sync(Utc("2026-01-01T00:00:00Z"), Now.AddHours(-1));
        var incremental = new WebullOrderWindow(Now.AddDays(-8), Now, null);

        Assert.Equal(Sync(sync.CoveredFrom, Now), _sut.Advance(sync, incremental, null));
    }

    [Fact]
    public void Advance_IncrementalReachingBeforeCoveredFrom_ExtendsBothEnds()
    {
        var sync = Sync(Utc("2026-01-01T00:00:00Z"), Utc("2026-01-03T00:00:00Z"));
        var incremental = new WebullOrderWindow(Utc("2025-12-27T00:00:00Z"), Now, null);

        Assert.Equal(Sync(incremental.Start, Now), _sut.Advance(sync, incremental, null));
    }

    [Fact]
    public void Advance_WindowInsideCoverage_NeverShrinksIt()
    {
        var sync = Sync(Utc("2020-01-01T00:00:00Z"), Now);

        Assert.Equal(sync, _sut.Advance(sync, Backfill2025, null));
    }

    // ── Fail ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(WebullErrorKind.Unreachable)]
    [InlineData(WebullErrorKind.KeyRejected)]
    [InlineData(WebullErrorKind.TokenRejected)]
    public void Fail_RecordsTheError_KeepsCoverageAndWindow_DropsTheKey(WebullErrorKind error)
    {
        var sync = Sync(Utc("2026-01-01T00:00:00Z"), Now, Backfill2025 with { PaginationKey = "pk=3" });

        Assert.Equal(sync with { Window = Backfill2025, LastError = error }, _sut.Fail(sync, error));
    }

    [Fact]
    public void Fail_NoWindowInProgress_StaysWithoutOne()
    {
        var sync = Sync(Utc("2026-01-01T00:00:00Z"), Now);

        Assert.Equal(sync with { LastError = WebullErrorKind.Unreachable }, _sut.Fail(sync, WebullErrorKind.Unreachable));
    }

    // ── BuildStatus ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildStatus_NoAccounts_NothingKnownButComplete()
    {
        var status = _sut.BuildStatus(WebullState.Connected, [], [Sync(Floor, Now, accountId: "GONE")]);

        Assert.Equal(new WebullTradeHistoryStatus(WebullState.Connected, null, null, true, null), status);
    }

    [Theory]
    [InlineData(WebullState.NotSetUp)]
    [InlineData(WebullState.NeedsVerification)]
    public void BuildStatus_CopiesTheState(WebullState state) =>
        Assert.Equal(state, _sut.BuildStatus(state, [], []).State);

    [Fact]
    public void BuildStatus_AccountWithoutSync_CountsAsNothingCovered()
    {
        var status = _sut.BuildStatus(WebullState.Connected, ["ACC1", "ACC2"], [Sync(Floor, Now)]);

        Assert.Equal(new WebullTradeHistoryStatus(WebullState.Connected, null, null, false, null), status);
    }

    [Fact]
    public void BuildStatus_AccountWithWindowInProgressButNoCoverage_NothingKnown()
    {
        var status = _sut.BuildStatus(WebullState.Connected, ["ACC1"], [Sync(window: Backfill2025 with { PaginationKey = "k" })]);

        Assert.Equal(new WebullTradeHistoryStatus(WebullState.Connected, null, null, false, null), status);
    }

    [Fact]
    public void BuildStatus_AsOfIsTheOldestCoveredTo_HistoryFromIsTheNewestCoveredFromInEasternTime()
    {
        IReadOnlyList<WebullOrderSync> syncs =
        [
            Sync(Utc("2020-03-01T12:00:00Z"), Now.AddMinutes(-2), accountId: "ACC1"),
            Sync(Utc("2026-01-01T00:00:00Z"), Now.AddMinutes(-30), accountId: "ACC2") // still Dec 31 in Eastern time
        ];

        var status = _sut.BuildStatus(WebullState.Connected, ["ACC1", "ACC2"], syncs);

        Assert.Equal(Now.AddMinutes(-30), status.AsOf);
        Assert.Equal(new DateOnly(2025, 12, 31), status.HistoryFrom);
        Assert.False(status.IsComplete);
    }

    [Fact]
    public void BuildStatus_EveryAccountAtTheFloor_Complete()
    {
        IReadOnlyList<WebullOrderSync> syncs = [Sync(Floor, Now, accountId: "ACC1"), Sync(Floor, Now, accountId: "ACC2")];

        Assert.True(_sut.BuildStatus(WebullState.Connected, ["ACC1", "ACC2"], syncs).IsComplete);
    }

    [Fact]
    public void BuildStatus_OneAccountJustShortOfTheFloor_NotComplete()
    {
        IReadOnlyList<WebullOrderSync> syncs = [Sync(Floor, Now, accountId: "ACC1"), Sync(Floor + Ms, Now, accountId: "ACC2")];

        Assert.False(_sut.BuildStatus(WebullState.Connected, ["ACC1", "ACC2"], syncs).IsComplete);
    }

    [Fact]
    public void BuildStatus_IgnoresSyncsForAccountsNoLongerCurrent()
    {
        IReadOnlyList<WebullOrderSync> syncs =
        [
            Sync(Utc("2026-01-01T00:00:00Z"), Now.AddMinutes(-5), accountId: "ACC1"),
            Sync(Floor, Utc("2020-01-01T00:00:00Z"), lastError: WebullErrorKind.TokenRejected, accountId: "CLOSED")
        ];

        var status = _sut.BuildStatus(WebullState.Connected, ["ACC1"], syncs);

        Assert.Equal(
            new WebullTradeHistoryStatus(WebullState.Connected, Now.AddMinutes(-5), new DateOnly(2025, 12, 31), false, null),
            status);
    }

    [Fact]
    public void BuildStatus_LastErrorIsTheFirstErrorAmongCurrentAccountsInOrder()
    {
        IReadOnlyList<WebullOrderSync> syncs =
        [
            Sync(Floor, Now, lastError: WebullErrorKind.KeyRejected, accountId: "ACC3"),
            Sync(Floor, Now, accountId: "ACC1"),
            Sync(Floor, Now, lastError: WebullErrorKind.Unreachable, accountId: "ACC2")
        ];

        Assert.Equal(WebullErrorKind.Unreachable, _sut.BuildStatus(WebullState.Connected, ["ACC1", "ACC2", "ACC3"], syncs).LastError);
    }
}
