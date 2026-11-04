using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Plaid;
using BudgetTracker.Domain.Webull;
using BudgetTracker.Server.Managers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BudgetTracker.Tests.Managers;

/// <summary>
/// Orchestration tests for <see cref="WebullManager"/> with strict mocks: what is fetched, computed and persisted for each
/// refresh decision; that only <see cref="WebullApiException"/> is caught and stored; and that nothing calls Webull when
/// it is not configured. The "never creates a token" check runs the real <see cref="WebullEngine"/> over every state.
/// </summary>
public class WebullManagerTests
{
    private const int UserId = 5;
    private const string Token = "tok-plain";

    private readonly Mock<IWebullEngine> _engine = new(MockBehavior.Strict);
    private readonly Mock<IWebullAccessor> _webull = new(MockBehavior.Strict);
    private readonly Mock<IWebullConnectionAccessor> _connections = new(MockBehavior.Strict);
    private readonly Mock<IWebullHoldingsEngine> _holdings = new(MockBehavior.Strict);
    private readonly Mock<ILogger<WebullManager>> _logger = new();

    private static readonly IReadOnlyList<WebullBalance> Balances = [new("ACC1", "Individual Cash", "CASH", 100m, 25m, "USD")];
    private static readonly IReadOnlyList<WebullAccountPortfolio> Portfolio =
        [new(Balances[0], 75m, -1.5m, 4m, [new WebullPosition("AAPL", "EQUITY", 1m, 75m, 71m, 4m, "USD")])];
    private static readonly DateOnly EasternToday = new(2026, 10, 23);

    private static readonly WebullSnapshot PendingSnapshot = new(WebullTokenStatus.Pending, DateTime.UtcNow, null, null, []);
    private static readonly WebullSnapshot NormalSnapshot = new(WebullTokenStatus.Normal, DateTime.UtcNow, null, null, []);
    private static readonly WebullSnapshot FinalSnapshot = new(WebullTokenStatus.Normal, DateTime.UtcNow, DateTime.UtcNow, null, Balances);

    private static WebullConnectionView View(WebullState state, bool canVerify = false) =>
        new(state, [], null, false, null, null, canVerify);

    private static IOptions<WebullOptions> Configured(bool configured = true) =>
        Options.Create(configured ? new WebullOptions { AppKey = "key", AppSecret = "secret" } : new WebullOptions());

    private WebullManager BuildSut(bool configured = true) =>
        new(_engine.Object, _holdings.Object, _webull.Object, _connections.Object, Configured(configured), _logger.Object);

    private void VerifyWarningLogged(WebullApiException exception) => _logger.Verify(l => l.Log(
        LogLevel.Warning,
        It.IsAny<EventId>(),
        It.IsAny<It.IsAnyType>(),
        exception,
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once());

    private static DateTime IsUtcNow() =>
        It.Is<DateTime>(d => d.Kind == DateTimeKind.Utc && (DateTime.UtcNow - d).Duration() < TimeSpan.FromMinutes(1));

    /// <summary>A successful fetch is saved with the engine's Eastern date for now.</summary>
    private void StubSavePortfolio(int userId = UserId)
    {
        _engine.Setup(e => e.ToEasternDate(IsUtcNow())).Returns(EasternToday);
        _connections.Setup(c => c.SavePortfolioAsync(userId, Portfolio, IsUtcNow(), EasternToday)).Returns(Task.CompletedTask);
    }

    private void StubFinalView(WebullSnapshot? snapshot, WebullConnectionView view) =>
        _engine.Setup(e => e.BuildView(true, snapshot, IsUtcNow())).Returns(view);

    // ── GetAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_NotConfigured_BuildsNotSetUpWithoutReadingAnything()
    {
        var view = View(WebullState.NotSetUp);
        _engine.Setup(e => e.BuildView(false, null, IsUtcNow())).Returns(view);

        Assert.Same(view, await BuildSut(configured: false).GetAsync(UserId));
    }

    [Fact]
    public async Task Get_Configured_BuildsViewFromStoredSnapshot_WithoutCallingWebull()
    {
        var view = View(WebullState.Connected);
        _connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(FinalSnapshot);
        StubFinalView(FinalSnapshot, view);

        Assert.Same(view, await BuildSut().GetAsync(UserId));
    }

    // ── RefreshAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_NotConfigured_MakesNoCalls()
    {
        var view = View(WebullState.NotSetUp);
        _engine.Setup(e => e.BuildView(false, null, IsUtcNow())).Returns(view);

        Assert.Same(view, await BuildSut(configured: false).RefreshAsync(UserId));
    }

    [Fact]
    public async Task Refresh_NothingDue_CallsNoWebullApi()
    {
        var view = View(WebullState.Connected);
        _connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(FinalSnapshot);
        _engine.Setup(e => e.DecideRefresh(FinalSnapshot, IsUtcNow())).Returns(WebullRefreshAction.None);
        StubFinalView(FinalSnapshot, view);

        Assert.Same(view, await BuildSut().RefreshAsync(UserId));
    }

    [Theory]
    [InlineData(WebullRefreshAction.CheckToken)]
    [InlineData(WebullRefreshAction.FetchBalances)]
    public async Task Refresh_TokenMissingOrUndecryptable_StoresTokenRejectedWithoutCallingWebull(WebullRefreshAction action)
    {
        foreach (var lookup in new[] { AccessTokenLookup.NotFound, AccessTokenLookup.Undecryptable })
        {
            _connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(NormalSnapshot);
            _engine.Setup(e => e.DecideRefresh(NormalSnapshot, IsUtcNow())).Returns(action);
            _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(lookup);
            _connections.Setup(c => c.SetLastErrorAsync(UserId, WebullErrorKind.TokenRejected)).Returns(Task.CompletedTask);
            StubFinalView(NormalSnapshot, View(WebullState.NeedsVerification, canVerify: true));

            var view = await BuildSut().RefreshAsync(UserId);

            Assert.Equal(WebullState.NeedsVerification, view.State);
        }
        _connections.Verify(c => c.SetLastErrorAsync(UserId, WebullErrorKind.TokenRejected), Times.Exactly(2));
    }

    [Fact]
    public async Task Refresh_PendingNowApproved_ChecksThenFetchesInTheSameCall()
    {
        var view = View(WebullState.Connected);
        _connections.SetupSequence(c => c.GetAsync(UserId))
            .ReturnsAsync(PendingSnapshot)
            .ReturnsAsync(NormalSnapshot)
            .ReturnsAsync(FinalSnapshot);
        _engine.Setup(e => e.DecideRefresh(PendingSnapshot, IsUtcNow())).Returns(WebullRefreshAction.CheckToken);
        _engine.Setup(e => e.DecideRefresh(NormalSnapshot, IsUtcNow())).Returns(WebullRefreshAction.FetchBalances);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _webull.Setup(w => w.CheckTokenAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(WebullTokenStatus.Normal);
        _connections.Setup(c => c.SetTokenStatusAsync(UserId, WebullTokenStatus.Normal)).Returns(Task.CompletedTask);
        _webull.Setup(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(Portfolio);
        StubSavePortfolio();
        StubFinalView(FinalSnapshot, view);

        Assert.Same(view, await BuildSut().RefreshAsync(UserId));
        _connections.Verify(c => c.SavePortfolioAsync(UserId, Portfolio, IsUtcNow(), EasternToday), Times.Once);
    }

    [Fact]
    public async Task Refresh_StillPendingAfterCheck_DoesNotCheckAgainOrFetch()
    {
        _connections.SetupSequence(c => c.GetAsync(UserId))
            .ReturnsAsync(PendingSnapshot)
            .ReturnsAsync(PendingSnapshot)
            .ReturnsAsync(PendingSnapshot);
        _engine.Setup(e => e.DecideRefresh(PendingSnapshot, IsUtcNow())).Returns(WebullRefreshAction.CheckToken);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _webull.Setup(w => w.CheckTokenAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(WebullTokenStatus.Pending);
        _connections.Setup(c => c.SetTokenStatusAsync(UserId, WebullTokenStatus.Pending)).Returns(Task.CompletedTask);
        StubFinalView(PendingSnapshot, View(WebullState.WaitingForApproval));

        await BuildSut().RefreshAsync(UserId);

        _webull.Verify(w => w.CheckTokenAsync(Token, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(WebullErrorKind.Unreachable)]
    [InlineData(WebullErrorKind.KeyRejected)]
    [InlineData(WebullErrorKind.TokenRejected)]
    public async Task Refresh_CheckFails_StoresErrorKind_LeavesStatusAndDoesNotFetch(WebullErrorKind kind)
    {
        _connections.SetupSequence(c => c.GetAsync(UserId)).ReturnsAsync(PendingSnapshot).ReturnsAsync(PendingSnapshot);
        _engine.Setup(e => e.DecideRefresh(PendingSnapshot, IsUtcNow())).Returns(WebullRefreshAction.CheckToken);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _webull.Setup(w => w.CheckTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WebullApiException(kind, null, "failed", null));
        _connections.Setup(c => c.SetLastErrorAsync(UserId, kind)).Returns(Task.CompletedTask);
        StubFinalView(PendingSnapshot, View(WebullState.WaitingForApproval));

        await BuildSut().RefreshAsync(UserId);

        _connections.Verify(c => c.SetLastErrorAsync(UserId, kind), Times.Once);
        _webull.Verify(w => w.CheckTokenAsync(Token, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_FetchDue_SavesBalancesAtNow()
    {
        var view = View(WebullState.Connected);
        _connections.SetupSequence(c => c.GetAsync(UserId)).ReturnsAsync(NormalSnapshot).ReturnsAsync(FinalSnapshot);
        _engine.Setup(e => e.DecideRefresh(NormalSnapshot, IsUtcNow())).Returns(WebullRefreshAction.FetchBalances);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _webull.Setup(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(Portfolio);
        StubSavePortfolio();
        StubFinalView(FinalSnapshot, view);

        Assert.Same(view, await BuildSut().RefreshAsync(UserId));
    }

    [Theory]
    [InlineData(WebullErrorKind.Unreachable)]
    [InlineData(WebullErrorKind.KeyRejected)]
    [InlineData(WebullErrorKind.TokenRejected)]
    public async Task Refresh_FetchFails_StoresErrorKindOnce_NoRetry_ReturnsView(WebullErrorKind kind)
    {
        var view = View(WebullState.Connected) with { LastError = kind };
        _connections.SetupSequence(c => c.GetAsync(UserId)).ReturnsAsync(NormalSnapshot).ReturnsAsync(NormalSnapshot);
        _engine.Setup(e => e.DecideRefresh(NormalSnapshot, IsUtcNow())).Returns(WebullRefreshAction.FetchBalances);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _webull.Setup(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WebullApiException(kind, null, "failed", null));
        _connections.Setup(c => c.SetLastErrorAsync(UserId, kind)).Returns(Task.CompletedTask);
        StubFinalView(NormalSnapshot, view);

        Assert.Same(view, await BuildSut().RefreshAsync(UserId));
        _webull.Verify(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_NonWebullException_IsNotSwallowed()
    {
        _connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(NormalSnapshot);
        _engine.Setup(e => e.DecideRefresh(NormalSnapshot, IsUtcNow())).Returns(WebullRefreshAction.FetchBalances);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _webull.Setup(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Webull request GET /x is not allowlisted."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildSut().RefreshAsync(UserId));
    }

    // ── StartVerificationAsync ──────────────────────────────────────────────

    [Theory]
    [InlineData(WebullState.WaitingForApproval)]
    [InlineData(WebullState.Connected)]
    public async Task StartVerification_CannotVerify_FailsWithoutCreatingToken(WebullState state)
    {
        _connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(NormalSnapshot);
        StubFinalView(NormalSnapshot, View(state, canVerify: false));

        var result = await BuildSut().StartVerificationAsync(UserId);

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task StartVerification_NotConfigured_FailsWithoutAnyCall()
    {
        _engine.Setup(e => e.BuildView(false, null, IsUtcNow())).Returns(View(WebullState.NotSetUp));

        var result = await BuildSut(configured: false).StartVerificationAsync(UserId);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task StartVerification_CreatesAndStoresToken_ThenRefreshes()
    {
        var connected = View(WebullState.Connected);
        _connections.SetupSequence(c => c.GetAsync(UserId))
            .ReturnsAsync((WebullSnapshot?)null)
            .ReturnsAsync(NormalSnapshot)
            .ReturnsAsync(FinalSnapshot);
        StubFinalView(null, View(WebullState.NotConnected, canVerify: true));
        _webull.Setup(w => w.CreateTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebullToken(Token, WebullTokenStatus.Normal));
        _connections.Setup(c => c.SaveTokenAsync(UserId, Token, WebullTokenStatus.Normal, IsUtcNow())).Returns(Task.CompletedTask);
        _engine.Setup(e => e.DecideRefresh(NormalSnapshot, IsUtcNow())).Returns(WebullRefreshAction.FetchBalances);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _webull.Setup(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(Portfolio);
        StubSavePortfolio();
        StubFinalView(FinalSnapshot, connected);

        var result = await BuildSut().StartVerificationAsync(UserId);

        Assert.True(result.IsSuccess);
        Assert.Same(connected, result.Value);
        _webull.Verify(w => w.CreateTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(WebullErrorKind.Unreachable)]
    [InlineData(WebullErrorKind.KeyRejected)]
    public async Task StartVerification_CreateFails_StoresErrorAndSucceedsWithStoredView(WebullErrorKind kind)
    {
        var after = View(WebullState.NotConnected, canVerify: true) with { LastError = kind };
        _connections.SetupSequence(c => c.GetAsync(UserId)).ReturnsAsync((WebullSnapshot?)null).ReturnsAsync(NormalSnapshot);
        StubFinalView(null, View(WebullState.NotConnected, canVerify: true));
        _webull.Setup(w => w.CreateTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WebullApiException(kind, "UNAUTHORIZED", "failed", System.Net.HttpStatusCode.Unauthorized));
        _connections.Setup(c => c.SetLastErrorAsync(UserId, kind)).Returns(Task.CompletedTask);
        StubFinalView(NormalSnapshot, after);

        var result = await BuildSut().StartVerificationAsync(UserId);

        Assert.True(result.IsSuccess);
        Assert.Same(after, result.Value);
        _webull.Verify(w => w.CreateTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartVerification_ProductionTokenPending_ReturnsWaitingWithExpiry_ChecksOnceWithoutFetching()
    {
        // Production: Create answers Pending (an SMS went out); the immediate Check is still Pending.
        WebullSnapshot? stored = null;
        DateTime createdAt = default;
        _connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(() => stored);
        _webull.Setup(w => w.CreateTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebullToken(Token, WebullTokenStatus.Pending));
        _connections.Setup(c => c.SaveTokenAsync(UserId, Token, WebullTokenStatus.Pending, IsUtcNow()))
            .Callback<int, string, WebullTokenStatus, DateTime>((_, _, status, at) =>
            {
                createdAt = at;
                stored = new WebullSnapshot(status, at, null, null, []);
            })
            .Returns(Task.CompletedTask);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _webull.Setup(w => w.CheckTokenAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(WebullTokenStatus.Pending);
        _connections.Setup(c => c.SetTokenStatusAsync(UserId, WebullTokenStatus.Pending)).Returns(Task.CompletedTask);

        var result = await new WebullManager(new WebullEngine(), new WebullHoldingsEngine(), _webull.Object, _connections.Object, Configured(), _logger.Object)
            .StartVerificationAsync(UserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(WebullState.WaitingForApproval, result.Value!.State);
        Assert.Equal(createdAt.AddMinutes(5), result.Value.ApprovalExpiresAt);
        _webull.Verify(w => w.CreateTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
        _webull.Verify(w => w.CheckTokenAsync(Token, It.IsAny<CancellationToken>()), Times.Once);
        _webull.Verify(w => w.GetPortfolioAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartVerification_SaveTokenFails_PropagatesWithoutCreatingASecondToken()
    {
        _connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync((WebullSnapshot?)null);
        _webull.Setup(w => w.CreateTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebullToken(Token, WebullTokenStatus.Pending));
        _connections.Setup(c => c.SaveTokenAsync(UserId, Token, WebullTokenStatus.Pending, IsUtcNow()))
            .ThrowsAsync(new DbUpdateException("database unavailable"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => new WebullManager(new WebullEngine(), new WebullHoldingsEngine(), _webull.Object, _connections.Object, Configured(), _logger.Object).StartVerificationAsync(UserId));

        _webull.Verify(w => w.CreateTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Diagnostics: every caught Webull failure is logged with its (sanitized) exception ──

    [Fact]
    public async Task StartVerification_CreateFails_LogsWarningWithTheException()
    {
        var error = new WebullApiException(WebullErrorKind.KeyRejected, "UNAUTHORIZED", "Webull /auth/tokens/create failed with HTTP 401: UNAUTHORIZED.", System.Net.HttpStatusCode.Unauthorized);
        _connections.SetupSequence(c => c.GetAsync(UserId)).ReturnsAsync((WebullSnapshot?)null).ReturnsAsync(NormalSnapshot);
        StubFinalView(null, View(WebullState.NotConnected, canVerify: true));
        _webull.Setup(w => w.CreateTokenAsync(It.IsAny<CancellationToken>())).ThrowsAsync(error);
        _connections.Setup(c => c.SetLastErrorAsync(UserId, WebullErrorKind.KeyRejected)).Returns(Task.CompletedTask);
        StubFinalView(NormalSnapshot, View(WebullState.Connected));

        await BuildSut().StartVerificationAsync(UserId);

        VerifyWarningLogged(error);
    }

    [Fact]
    public async Task Refresh_CheckFails_LogsWarningWithTheException()
    {
        var error = new WebullApiException(WebullErrorKind.TokenRejected, "INVALID_TOKEN", "Webull /auth/tokens/check failed with HTTP 417: INVALID_TOKEN.", System.Net.HttpStatusCode.ExpectationFailed);
        _connections.SetupSequence(c => c.GetAsync(UserId)).ReturnsAsync(PendingSnapshot).ReturnsAsync(PendingSnapshot);
        _engine.Setup(e => e.DecideRefresh(PendingSnapshot, IsUtcNow())).Returns(WebullRefreshAction.CheckToken);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _webull.Setup(w => w.CheckTokenAsync(Token, It.IsAny<CancellationToken>())).ThrowsAsync(error);
        _connections.Setup(c => c.SetLastErrorAsync(UserId, WebullErrorKind.TokenRejected)).Returns(Task.CompletedTask);
        StubFinalView(PendingSnapshot, View(WebullState.NeedsVerification, canVerify: true));

        await BuildSut().RefreshAsync(UserId);

        VerifyWarningLogged(error);
    }

    [Fact]
    public async Task Refresh_FetchFails_LogsWarningWithTheException()
    {
        var error = new WebullApiException(WebullErrorKind.Unreachable, "SYSTEM_ERROR", "Webull /trading/accounts/list failed with HTTP 500: SYSTEM_ERROR.", System.Net.HttpStatusCode.InternalServerError);
        _connections.SetupSequence(c => c.GetAsync(UserId)).ReturnsAsync(NormalSnapshot).ReturnsAsync(NormalSnapshot);
        _engine.Setup(e => e.DecideRefresh(NormalSnapshot, IsUtcNow())).Returns(WebullRefreshAction.FetchBalances);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _webull.Setup(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>())).ThrowsAsync(error);
        _connections.Setup(c => c.SetLastErrorAsync(UserId, WebullErrorKind.Unreachable)).Returns(Task.CompletedTask);
        StubFinalView(NormalSnapshot, View(WebullState.Connected));

        await BuildSut().RefreshAsync(UserId);

        VerifyWarningLogged(error);
    }

    // ── Refresh never creates a token (real engine, every state) ────────────

    [Fact]
    public async Task Refresh_NeverCreatesAToken_InAnyState()
    {
        var now = DateTime.UtcNow;
        WebullTokenStatus?[] statuses = [null, .. Enum.GetValues<WebullTokenStatus>()];
        WebullErrorKind?[] errors = [null, .. Enum.GetValues<WebullErrorKind>()];
        DateTime[] createdAts = [now, now.AddMinutes(-10), now.AddDays(-30)];
        DateTime?[] fetchedAts = [null, now.AddMinutes(-1), now.AddHours(-2), now.AddDays(-40)];
        List<WebullSnapshot?> snapshots = [null];
        foreach (var status in statuses)
        foreach (var error in errors)
        foreach (var createdAt in createdAts)
        foreach (var fetchedAt in fetchedAts)
            snapshots.Add(new WebullSnapshot(status, status is null ? null : createdAt, fetchedAt, error, []));

        var webull = new Mock<IWebullAccessor>();
        webull.Setup(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(Portfolio);
        foreach (var checkResult in Enum.GetValues<WebullTokenStatus>())
        foreach (var snapshot in snapshots)
        foreach (var configured in new[] { true, false })
        {
            var connections = new Mock<IWebullConnectionAccessor>();
            connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(snapshot);
            connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
            webull.Setup(w => w.CheckTokenAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(checkResult);

            await new WebullManager(new WebullEngine(), new WebullHoldingsEngine(), webull.Object, connections.Object, Configured(configured), NullLogger<WebullManager>.Instance).RefreshAsync(UserId);
        }

        webull.Verify(w => w.CreateTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
        // The loop really exercised the Webull paths, so "never created" is not vacuous.
        webull.Verify(w => w.CheckTokenAsync(Token, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        webull.Verify(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ── GetHoldingsAsync ────────────────────────────────────────────────────

    private static readonly WebullHoldingsView HoldingsView = new(WebullState.Connected, DateTime.UtcNow, false, null, []);

    [Fact]
    public async Task GetHoldings_NotConfigured_NotSetUpWithoutReadingAnything()
    {
        var notSetUp = View(WebullState.NotSetUp);
        _engine.Setup(e => e.BuildView(false, null, IsUtcNow())).Returns(notSetUp);
        _holdings.Setup(h => h.BuildView(notSetUp, It.Is<IReadOnlyList<WebullAccountPortfolio>>(a => a.Count == 0))).Returns(HoldingsView);

        Assert.Same(HoldingsView, await BuildSut(configured: false).GetHoldingsAsync(UserId));
    }

    [Fact]
    public async Task GetHoldings_ValuesTheSavedPortfolioWithTheV1View_WithoutCallingWebull()
    {
        var connected = View(WebullState.Connected);
        _connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(FinalSnapshot);
        StubFinalView(FinalSnapshot, connected);
        _connections.Setup(c => c.GetSavedPortfolioAsync(UserId)).ReturnsAsync(Portfolio);
        _holdings.Setup(h => h.BuildView(connected, Portfolio)).Returns(HoldingsView);

        Assert.Same(HoldingsView, await BuildSut().GetHoldingsAsync(UserId));
    }

    // ── RunCloseSnapshotsAsync ──────────────────────────────────────────────

    private const int OtherUserId = 6;

    /// <summary>The close run's catch-all would hide a strict-mock failure, so tests also assert nothing was logged with an exception.</summary>
    private void VerifyNoExceptionLogged() => _logger.Verify(l => l.Log(
        It.IsIn(LogLevel.Warning, LogLevel.Error),
        It.IsAny<EventId>(),
        It.IsAny<It.IsAnyType>(),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never());

    private void StubCloseRun(int userId, WebullSnapshot snapshot, bool due)
    {
        _connections.Setup(c => c.GetAsync(userId)).ReturnsAsync(snapshot);
        _engine.Setup(e => e.IsCloseRunDue(snapshot, IsUtcNow())).Returns(due);
    }

    [Fact]
    public async Task RunCloseSnapshots_NotConfigured_DoesNothing()
    {
        await BuildSut(configured: false).RunCloseSnapshotsAsync();
    }

    [Fact]
    public async Task RunCloseSnapshots_FetchesAndSavesForDueUsers_SkipsTheRest()
    {
        var otherSnapshot = NormalSnapshot with { LastFetchedAt = DateTime.UtcNow };
        _connections.Setup(c => c.GetUserIdsWithTokenAsync()).ReturnsAsync([UserId, OtherUserId]);
        StubCloseRun(UserId, NormalSnapshot, due: true);
        StubCloseRun(OtherUserId, otherSnapshot, due: false);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _webull.Setup(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(Portfolio);
        StubSavePortfolio();

        await BuildSut().RunCloseSnapshotsAsync();

        _connections.Verify(c => c.SavePortfolioAsync(UserId, Portfolio, IsUtcNow(), EasternToday), Times.Once);
        _connections.Verify(c => c.GetAccessTokenAsync(OtherUserId), Times.Never);
        VerifyNoExceptionLogged();
    }

    [Fact]
    public async Task RunCloseSnapshots_TokenUnreadable_StoresTokenRejected_WithoutCallingWebull()
    {
        _connections.Setup(c => c.GetUserIdsWithTokenAsync()).ReturnsAsync([UserId]);
        StubCloseRun(UserId, NormalSnapshot, due: true);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Undecryptable);
        _connections.Setup(c => c.SetLastErrorAsync(UserId, WebullErrorKind.TokenRejected)).Returns(Task.CompletedTask);

        await BuildSut().RunCloseSnapshotsAsync();

        _connections.Verify(c => c.SetLastErrorAsync(UserId, WebullErrorKind.TokenRejected), Times.Once);
        _webull.VerifyNoOtherCalls();
        VerifyNoExceptionLogged();
    }

    [Fact]
    public async Task RunCloseSnapshots_WebullFailure_IsLoggedAndStored_AndTheNextUserStillRuns()
    {
        var error = new WebullApiException(WebullErrorKind.Unreachable, "SYSTEM_ERROR", "Webull /trading/assets/positions/list failed with HTTP 500: SYSTEM_ERROR.", System.Net.HttpStatusCode.InternalServerError);
        _connections.Setup(c => c.GetUserIdsWithTokenAsync()).ReturnsAsync([UserId, OtherUserId]);
        StubCloseRun(UserId, NormalSnapshot, due: true);
        StubCloseRun(OtherUserId, PendingSnapshot, due: true);
        _connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));
        _connections.Setup(c => c.GetAccessTokenAsync(OtherUserId)).ReturnsAsync(AccessTokenLookup.Found("other-token"));
        _webull.Setup(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>())).ThrowsAsync(error);
        _webull.Setup(w => w.GetPortfolioAsync("other-token", It.IsAny<CancellationToken>())).ReturnsAsync(Portfolio);
        _connections.Setup(c => c.SetLastErrorAsync(UserId, WebullErrorKind.Unreachable)).Returns(Task.CompletedTask);
        StubSavePortfolio(OtherUserId);

        await BuildSut().RunCloseSnapshotsAsync();

        VerifyWarningLogged(error);
        _connections.Verify(c => c.SetLastErrorAsync(UserId, WebullErrorKind.Unreachable), Times.Once);
        _connections.Verify(c => c.SavePortfolioAsync(UserId, It.IsAny<IReadOnlyList<WebullAccountPortfolio>>(), It.IsAny<DateTime>(), It.IsAny<DateOnly>()), Times.Never);
        _connections.Verify(c => c.SavePortfolioAsync(OtherUserId, Portfolio, IsUtcNow(), EasternToday), Times.Once);
    }

    [Fact]
    public async Task RunCloseSnapshots_UnexpectedFailureForOneUser_IsLogged_AndTheNextUserStillRuns()
    {
        // e.g. a key race with a manual refresh saving the same day's row.
        var race = new DbUpdateException("duplicate key value violates unique constraint \"PK_WebullDailyValues\"");
        _connections.Setup(c => c.GetUserIdsWithTokenAsync()).ReturnsAsync([UserId, OtherUserId]);
        StubCloseRun(UserId, NormalSnapshot, due: true);
        StubCloseRun(OtherUserId, PendingSnapshot, due: true);
        _connections.Setup(c => c.GetAccessTokenAsync(It.IsAny<int>())).ReturnsAsync(AccessTokenLookup.Found(Token));
        _webull.Setup(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(Portfolio);
        _engine.Setup(e => e.ToEasternDate(IsUtcNow())).Returns(EasternToday);
        _connections.Setup(c => c.SavePortfolioAsync(UserId, Portfolio, IsUtcNow(), EasternToday)).ThrowsAsync(race);
        _connections.Setup(c => c.SavePortfolioAsync(OtherUserId, Portfolio, IsUtcNow(), EasternToday)).Returns(Task.CompletedTask);

        await BuildSut().RunCloseSnapshotsAsync();

        _logger.Verify(l => l.Log(
            It.IsIn(LogLevel.Warning, LogLevel.Error),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            race,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once());
        _connections.Verify(c => c.SavePortfolioAsync(OtherUserId, Portfolio, IsUtcNow(), EasternToday), Times.Once);
    }

    [Fact]
    public async Task RunCloseSnapshots_NeverCreatesOrChecksAToken_InAnyState()
    {
        var now = DateTime.UtcNow;
        WebullTokenStatus?[] statuses = [null, .. Enum.GetValues<WebullTokenStatus>()];
        WebullErrorKind?[] errors = [null, .. Enum.GetValues<WebullErrorKind>()];
        DateTime[] createdAts = [now, now.AddMinutes(-10), now.AddDays(-30)];
        DateTime?[] fetchedAts = [null, now.AddMinutes(-1), now.AddHours(-2), now.AddDays(-40)];
        List<WebullSnapshot?> snapshots = [null];
        foreach (var status in statuses)
        foreach (var error in errors)
        foreach (var createdAt in createdAts)
        foreach (var fetchedAt in fetchedAts)
            snapshots.Add(new WebullSnapshot(status, status is null ? null : createdAt, fetchedAt, error, []));

        var webull = new Mock<IWebullAccessor>();
        webull.Setup(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(Portfolio);
        foreach (var snapshot in snapshots)
        foreach (var configured in new[] { true, false })
        {
            var connections = new Mock<IWebullConnectionAccessor>();
            connections.Setup(c => c.GetUserIdsWithTokenAsync()).ReturnsAsync([UserId]);
            connections.Setup(c => c.GetAsync(UserId)).ReturnsAsync(snapshot);
            connections.Setup(c => c.GetAccessTokenAsync(UserId)).ReturnsAsync(AccessTokenLookup.Found(Token));

            await new WebullManager(new WebullEngine(), new WebullHoldingsEngine(), webull.Object, connections.Object, Configured(configured), NullLogger<WebullManager>.Instance)
                .RunCloseSnapshotsAsync();
        }

        webull.Verify(w => w.CreateTokenAsync(It.IsAny<CancellationToken>()), Times.Never);
        webull.Verify(w => w.CheckTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        // Some states were due, so "never created or checked" is not vacuous.
        webull.Verify(w => w.GetPortfolioAsync(Token, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
