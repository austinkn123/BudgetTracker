using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Tests.Engines;

/// <summary>
/// Tests for <see cref="WebullEngine"/>: the refresh decision and the view mapping, with every time boundary
/// (5-minute approval window, 15-minute fetch threshold, 24-hour staleness) checked on both sides.
/// </summary>
public class WebullEngineTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    private static readonly IReadOnlyList<WebullBalance> SomeBalances =
        [new WebullBalance("ACC1", "Individual Cash", "CASH", 100m, 25m, "USD")];

    private readonly WebullEngine _sut = new();

    private static WebullSnapshot Snapshot(
        WebullTokenStatus? status = WebullTokenStatus.Normal,
        DateTime? createdAt = null,
        DateTime? fetchedAt = null,
        WebullErrorKind? lastError = null,
        IReadOnlyList<WebullBalance>? balances = null) =>
        new(status, status is null ? null : createdAt ?? Now.AddDays(-1), fetchedAt, lastError, balances ?? []);

    // ── DecideRefresh: None ─────────────────────────────────────────────────

    [Fact]
    public void DecideRefresh_NothingStored_None() =>
        Assert.Equal(WebullRefreshAction.None, _sut.DecideRefresh(null, Now));

    [Fact]
    public void DecideRefresh_NoToken_None() =>
        Assert.Equal(WebullRefreshAction.None, _sut.DecideRefresh(Snapshot(status: null), Now));

    [Theory]
    [InlineData(WebullTokenStatus.Expired)]
    [InlineData(WebullTokenStatus.Invalid)]
    public void DecideRefresh_ExpiredOrInvalidToken_None(WebullTokenStatus status) =>
        Assert.Equal(WebullRefreshAction.None, _sut.DecideRefresh(Snapshot(status), Now));

    [Theory]
    [InlineData(WebullTokenStatus.Normal)]
    [InlineData(WebullTokenStatus.Pending)]
    public void DecideRefresh_TokenRejected_NoneEvenWhenDueOrPending(WebullTokenStatus status) =>
        Assert.Equal(WebullRefreshAction.None,
            _sut.DecideRefresh(Snapshot(status, lastError: WebullErrorKind.TokenRejected), Now));

    // ── DecideRefresh: CheckToken ───────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(60 * 24 * 30)]
    public void DecideRefresh_PendingToken_ChecksAtAnyAge(int minutesOld) =>
        Assert.Equal(WebullRefreshAction.CheckToken,
            _sut.DecideRefresh(Snapshot(WebullTokenStatus.Pending, createdAt: Now.AddMinutes(-minutesOld)), Now));

    [Theory]
    [InlineData(WebullErrorKind.Unreachable)]
    [InlineData(WebullErrorKind.KeyRejected)]
    public void DecideRefresh_PendingTokenAfterNonTokenError_StillChecks(WebullErrorKind error) =>
        Assert.Equal(WebullRefreshAction.CheckToken,
            _sut.DecideRefresh(Snapshot(WebullTokenStatus.Pending, createdAt: Now, lastError: error), Now));

    // ── DecideRefresh: FetchBalances ────────────────────────────────────────

    [Fact]
    public void DecideRefresh_NormalNeverFetched_Fetches() =>
        Assert.Equal(WebullRefreshAction.FetchBalances, _sut.DecideRefresh(Snapshot(fetchedAt: null), Now));

    [Fact]
    public void DecideRefresh_NormalFetchedExactly15MinutesAgo_Fetches() =>
        Assert.Equal(WebullRefreshAction.FetchBalances,
            _sut.DecideRefresh(Snapshot(fetchedAt: Now.AddMinutes(-15)), Now));

    [Fact]
    public void DecideRefresh_NormalFetchedJustUnder15MinutesAgo_None() =>
        Assert.Equal(WebullRefreshAction.None,
            _sut.DecideRefresh(Snapshot(fetchedAt: Now.AddMinutes(-15) + Tick), Now));

    [Fact]
    public void DecideRefresh_NormalFetchedBeforeTokenCreated_FetchesEvenIfRecent()
    {
        var createdAt = Now.AddMinutes(-1);

        Assert.Equal(WebullRefreshAction.FetchBalances,
            _sut.DecideRefresh(Snapshot(createdAt: createdAt, fetchedAt: createdAt - Tick), Now));
    }

    [Fact]
    public void DecideRefresh_NormalFetchedAtTokenCreation_None()
    {
        var createdAt = Now.AddMinutes(-1);

        Assert.Equal(WebullRefreshAction.None,
            _sut.DecideRefresh(Snapshot(createdAt: createdAt, fetchedAt: createdAt), Now));
    }

    [Theory]
    [InlineData(WebullErrorKind.Unreachable)]
    [InlineData(WebullErrorKind.KeyRejected)]
    public void DecideRefresh_NormalDueAfterNonTokenError_Fetches(WebullErrorKind error) =>
        Assert.Equal(WebullRefreshAction.FetchBalances,
            _sut.DecideRefresh(Snapshot(fetchedAt: Now.AddHours(-1), lastError: error), Now));

    // ── BuildView: state mapping ────────────────────────────────────────────

    [Fact]
    public void BuildView_NotConfigured_NotSetUpWithNothingShown()
    {
        var view = _sut.BuildView(false, Snapshot(fetchedAt: Now.AddDays(-3), lastError: WebullErrorKind.Unreachable, balances: SomeBalances), Now);

        Assert.Equal(WebullState.NotSetUp, view.State);
        Assert.Empty(view.Balances);
        Assert.Null(view.AsOf);
        Assert.False(view.IsStale);
        Assert.Null(view.LastError);
        Assert.Null(view.ApprovalExpiresAt);
        Assert.False(view.CanVerify);
    }

    [Fact]
    public void BuildView_NothingStored_NotConnected()
    {
        var view = _sut.BuildView(true, null, Now);

        Assert.Equal(WebullState.NotConnected, view.State);
        Assert.Empty(view.Balances);
        Assert.Null(view.AsOf);
        Assert.False(view.IsStale);
        Assert.Null(view.LastError);
        Assert.Null(view.ApprovalExpiresAt);
    }

    [Fact]
    public void BuildView_NoToken_NotConnected() =>
        Assert.Equal(WebullState.NotConnected, _sut.BuildView(true, Snapshot(status: null), Now).State);

    [Theory]
    [InlineData(WebullTokenStatus.Normal)]
    [InlineData(WebullTokenStatus.Pending)]
    [InlineData(WebullTokenStatus.Expired)]
    public void BuildView_TokenRejected_NeedsVerificationWhateverTheStatus(WebullTokenStatus status) =>
        Assert.Equal(WebullState.NeedsVerification,
            _sut.BuildView(true, Snapshot(status, createdAt: Now, lastError: WebullErrorKind.TokenRejected), Now).State);

    [Fact]
    public void BuildView_InvalidToken_NeedsVerification() =>
        Assert.Equal(WebullState.NeedsVerification, _sut.BuildView(true, Snapshot(WebullTokenStatus.Invalid), Now).State);

    [Fact]
    public void BuildView_PendingJustInsideWindow_WaitingWithExpiry()
    {
        var createdAt = Now.AddMinutes(-5) + Tick;

        var view = _sut.BuildView(true, Snapshot(WebullTokenStatus.Pending, createdAt: createdAt), Now);

        Assert.Equal(WebullState.WaitingForApproval, view.State);
        Assert.Equal(createdAt.AddMinutes(5), view.ApprovalExpiresAt);
    }

    [Fact]
    public void BuildView_PendingAtExactlyFiveMinutes_ApprovalExpired()
    {
        var view = _sut.BuildView(true, Snapshot(WebullTokenStatus.Pending, createdAt: Now.AddMinutes(-5)), Now);

        Assert.Equal(WebullState.ApprovalExpired, view.State);
        Assert.Null(view.ApprovalExpiresAt);
        Assert.True(view.CanVerify);
    }

    [Fact]
    public void BuildView_ExpiredToken_ApprovalExpired() =>
        Assert.Equal(WebullState.ApprovalExpired, _sut.BuildView(true, Snapshot(WebullTokenStatus.Expired), Now).State);

    [Theory]
    [InlineData(null)]
    [InlineData(WebullErrorKind.Unreachable)]
    [InlineData(WebullErrorKind.KeyRejected)]
    public void BuildView_NormalToken_Connected(WebullErrorKind? lastError)
    {
        var view = _sut.BuildView(true, Snapshot(lastError: lastError), Now);

        Assert.Equal(WebullState.Connected, view.State);
        Assert.Equal(lastError, view.LastError);
        Assert.Null(view.ApprovalExpiresAt);
    }

    // ── BuildView: CanVerify ────────────────────────────────────────────────

    public static TheoryData<WebullSnapshot?, bool, bool> CanVerifyCases => new()
    {
        { null, false, false },                                                    // NotSetUp
        { null, true, true },                                                      // NotConnected
        { Snapshot(WebullTokenStatus.Pending, createdAt: Now), true, false },     // WaitingForApproval
        { Snapshot(WebullTokenStatus.Normal), true, false },                      // Connected
        { Snapshot(WebullTokenStatus.Expired), true, true },                      // ApprovalExpired
        { Snapshot(WebullTokenStatus.Invalid), true, true },                      // NeedsVerification
        { Snapshot(WebullTokenStatus.Pending, createdAt: Now.AddMinutes(-5)), true, true },                  // ApprovalExpired (window over)
        { Snapshot(WebullTokenStatus.Normal, lastError: WebullErrorKind.TokenRejected), true, true },        // NeedsVerification
        // Deliberately pinned: a rejected App Key stays Connected with no Verify offered. Revisit after the live harness.
        { Snapshot(WebullTokenStatus.Normal, lastError: WebullErrorKind.KeyRejected), true, false }
    };

    [Theory]
    [MemberData(nameof(CanVerifyCases))]
    public void BuildView_CanVerifyOnlyWhenNotConnectedExpiredOrNeedsVerification(WebullSnapshot? snapshot, bool isConfigured, bool canVerify) =>
        Assert.Equal(canVerify, _sut.BuildView(isConfigured, snapshot, Now).CanVerify);

    // ── BuildView: balances and staleness ───────────────────────────────────

    public static TheoryData<WebullSnapshot> NonConnectedSnapshots => new()
    {
        Snapshot(WebullTokenStatus.Expired, fetchedAt: Now.AddHours(-1), balances: SomeBalances),
        Snapshot(WebullTokenStatus.Invalid, fetchedAt: Now.AddHours(-1), balances: SomeBalances),
        Snapshot(WebullTokenStatus.Normal, fetchedAt: Now.AddHours(-1), lastError: WebullErrorKind.TokenRejected, balances: SomeBalances),
        Snapshot(WebullTokenStatus.Pending, createdAt: Now.AddMinutes(-5), fetchedAt: Now.AddHours(-1), balances: SomeBalances),
        Snapshot(WebullTokenStatus.Pending, createdAt: Now, fetchedAt: Now.AddHours(-1), balances: SomeBalances)
    };

    [Theory]
    [MemberData(nameof(NonConnectedSnapshots))]
    public void BuildView_NonConnectedStates_KeepLastBalancesAndAsOf(WebullSnapshot snapshot)
    {
        var view = _sut.BuildView(true, snapshot, Now);

        Assert.NotEqual(WebullState.Connected, view.State);
        Assert.Same(SomeBalances, view.Balances);
        Assert.Equal(Now.AddHours(-1), view.AsOf);
    }

    [Fact]
    public void BuildView_PassesThroughBalancesAndFetchTime()
    {
        var fetchedAt = Now.AddMinutes(-3);

        var view = _sut.BuildView(true, Snapshot(fetchedAt: fetchedAt, balances: SomeBalances), Now);

        Assert.Same(SomeBalances, view.Balances);
        Assert.Equal(fetchedAt, view.AsOf);
    }

    [Fact]
    public void BuildView_FetchedExactly24HoursAgo_NotStale() =>
        Assert.False(_sut.BuildView(true, Snapshot(fetchedAt: Now.AddHours(-24)), Now).IsStale);

    [Fact]
    public void BuildView_FetchedJustOver24HoursAgo_Stale() =>
        Assert.True(_sut.BuildView(true, Snapshot(fetchedAt: Now.AddHours(-24) - Tick), Now).IsStale);

    [Fact]
    public void BuildView_NeverFetched_NotStale() =>
        Assert.False(_sut.BuildView(true, Snapshot(fetchedAt: null), Now).IsStale);

    // ── ToEasternDate: both sides of local midnight around each DST change ──

    [Theory]
    [InlineData("2026-03-08T04:59:00Z", "2026-03-07")] // EST: 23:59 Sat
    [InlineData("2026-03-08T05:00:00Z", "2026-03-08")] // EST: 00:00 Sun, the day DST starts
    [InlineData("2026-03-09T03:59:00Z", "2026-03-08")] // EDT: 23:59 Sun
    [InlineData("2026-03-09T04:00:00Z", "2026-03-09")] // EDT: 00:00 Mon
    [InlineData("2026-11-01T03:59:00Z", "2026-10-31")] // EDT: 23:59 Sat
    [InlineData("2026-11-01T04:00:00Z", "2026-11-01")] // EDT: 00:00 Sun, the day DST ends
    [InlineData("2026-11-02T04:59:00Z", "2026-11-01")] // EST: 23:59 Sun
    [InlineData("2026-11-02T05:00:00Z", "2026-11-02")] // EST: 00:00 Mon
    [InlineData("2026-10-31T00:30:00Z", "2026-10-30")] // EDT: 20:30 Fri, the UTC date is already Saturday
    public void ToEasternDate_FollowsUsDaylightSavingRule(string utc, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), _sut.ToEasternDate(Utc(utc)));

    // ── IsCloseRunDue ───────────────────────────────────────────────────────

    private static DateTime Utc(string iso) =>
        DateTime.Parse(iso, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal);

    private static WebullSnapshot Fetched(DateTime? fetchedAt, WebullTokenStatus? status = WebullTokenStatus.Normal, WebullErrorKind? lastError = null) =>
        new(status, status is null ? null : Utc("2026-01-01T00:00:00Z"), fetchedAt, lastError, []);

    [Theory]
    [InlineData("2026-03-06T21:30:00Z")] // Fri, EST
    [InlineData("2026-03-09T20:30:00Z")] // Mon, EDT (first weekday after DST starts)
    [InlineData("2026-10-30T20:30:00Z")] // Fri, EDT
    [InlineData("2026-11-02T21:30:00Z")] // Mon, EST (first weekday after DST ends)
    public void IsCloseRunDue_BecomesDueExactlyAtTheFourThirtyEasternClose(string closeUtc)
    {
        var close = Utc(closeUtc);
        var fetchedJustBefore = Fetched(close.AddMinutes(-1)); // after the previous close

        Assert.False(_sut.IsCloseRunDue(fetchedJustBefore, close - Tick));
        Assert.True(_sut.IsCloseRunDue(fetchedJustBefore, close));
    }

    [Fact]
    public void IsCloseRunDue_FetchedAtOrAfterTheLatestClose_NotDue()
    {
        var close = Utc("2026-03-09T20:30:00Z");

        Assert.False(_sut.IsCloseRunDue(Fetched(close), close.AddHours(3)));
        Assert.False(_sut.IsCloseRunDue(Fetched(close.AddMinutes(1)), close.AddHours(3)));
        Assert.True(_sut.IsCloseRunDue(Fetched(close - Tick), close.AddHours(3)));
    }

    [Fact]
    public void IsCloseRunDue_NormalNeverFetched_Due() =>
        Assert.True(_sut.IsCloseRunDue(Fetched(null), Utc("2026-03-09T12:00:00Z")));

    [Theory]
    [InlineData("2026-03-07T22:00:00Z")] // Sat 5 pm EST: Saturday is not a close
    [InlineData("2026-03-08T21:00:00Z")] // Sun 5 pm EDT: Sunday is not a close
    [InlineData("2026-03-09T13:00:00Z")] // Mon 9 am EDT: before Monday's close
    public void IsCloseRunDue_OverTheWeekend_LatestCloseIsFriday(string nowUtc)
    {
        var fridayClose = Utc("2026-03-06T21:30:00Z");

        Assert.False(_sut.IsCloseRunDue(Fetched(fridayClose.AddMinutes(1)), Utc(nowUtc)));
        Assert.True(_sut.IsCloseRunDue(Fetched(fridayClose.AddMinutes(-1)), Utc(nowUtc)));
    }

    [Theory]
    [InlineData(WebullTokenStatus.Pending)]
    [InlineData(WebullTokenStatus.Expired)]
    [InlineData(WebullTokenStatus.Invalid)]
    public void IsCloseRunDue_TokenNotNormal_NeverDue(WebullTokenStatus status) =>
        Assert.False(_sut.IsCloseRunDue(Fetched(null, status), Utc("2026-03-09T21:00:00Z")));

    [Fact]
    public void IsCloseRunDue_NothingStoredOrNoToken_NeverDue()
    {
        Assert.False(_sut.IsCloseRunDue(null, Utc("2026-03-09T21:00:00Z")));
        Assert.False(_sut.IsCloseRunDue(Fetched(null, status: null), Utc("2026-03-09T21:00:00Z")));
    }

    [Fact]
    public void IsCloseRunDue_TokenRejected_NeverDue() =>
        Assert.False(_sut.IsCloseRunDue(Fetched(null, lastError: WebullErrorKind.TokenRejected), Utc("2026-03-09T21:00:00Z")));

    [Theory]
    [InlineData(WebullErrorKind.Unreachable)]
    [InlineData(WebullErrorKind.KeyRejected)]
    public void IsCloseRunDue_AfterNonTokenError_StillDue(WebullErrorKind error) =>
        Assert.True(_sut.IsCloseRunDue(Fetched(null, lastError: error), Utc("2026-03-09T21:00:00Z")));

    public static TheoryData<WebullTokenStatus?, WebullErrorKind?> EveryTokenStatusAndError()
    {
        var data = new TheoryData<WebullTokenStatus?, WebullErrorKind?>();
        WebullTokenStatus?[] statuses = [null, .. Enum.GetValues<WebullTokenStatus>()];
        WebullErrorKind?[] errors = [null, .. Enum.GetValues<WebullErrorKind>()];
        foreach (var status in statuses)
        foreach (var error in errors)
            data.Add(status, error);
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryTokenStatusAndError))]
    public void IsCloseRunDue_OnlyNormalWithoutTokenRejected_IsEverDue(WebullTokenStatus? status, WebullErrorKind? error)
    {
        var expected = status == WebullTokenStatus.Normal && error != WebullErrorKind.TokenRejected;

        Assert.Equal(expected, _sut.IsCloseRunDue(Fetched(null, status, error), Utc("2026-03-09T21:00:00Z")));
    }

    [Theory]
    [InlineData("2026-10-31T22:00:00Z")] // Sat 6 pm EDT
    [InlineData("2026-11-01T22:00:00Z")] // Sun 5 pm EST: DST ended this morning
    [InlineData("2026-11-02T14:00:00Z")] // Mon 9 am EST: before Monday's close
    public void IsCloseRunDue_WeekendAcrossDstEnd_LatestCloseIsFridayEdt(string nowUtc)
    {
        // Friday's close was 4:30 pm EDT = 20:30Z, even though "now" is in EST.
        Assert.False(_sut.IsCloseRunDue(Fetched(Utc("2026-10-30T20:31:00Z")), Utc(nowUtc)));
        Assert.True(_sut.IsCloseRunDue(Fetched(Utc("2026-10-30T20:29:00Z")), Utc(nowUtc)));
    }

    [Fact]
    public void IsCloseRunDue_MarketHoliday_IsStillAClose()
    {
        // Thanksgiving: deliberately not excluded, so the run fetches (unchanged) holiday values once.
        Assert.True(_sut.IsCloseRunDue(Fetched(Utc("2026-11-25T21:31:00Z")), Utc("2026-11-26T21:30:00Z")));
    }
}
