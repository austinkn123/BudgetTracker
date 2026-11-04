using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;
using BudgetTracker.Domain.Webull;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BudgetTracker.Tests.Accessors;

/// <summary>
/// Tests for <see cref="WebullConnectionAccessor"/> against the InMemory provider: get-or-create on every write, token
/// encryption under its own Data Protection purpose, the balance snapshot diff, and UTC kinds on read. Check constraints
/// are not enforced by InMemory; they were verified against Postgres when the migration was applied.
/// </summary>
public class WebullConnectionAccessorTests : IDisposable
{
    private const int UserId = 5;
    private const int OtherUserId = 6;
    private static readonly DateTime CreatedAt = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FetchedAt = new(2026, 10, 22, 12, 30, 0, DateTimeKind.Utc);

    private readonly BudgetTrackerDbContext _context;
    private readonly SaveCounter _saves = new();
    private readonly EphemeralDataProtectionProvider _keyRing = new();

    /// <summary>Counts SaveChanges calls.</summary>
    private sealed class SaveCounter : SaveChangesInterceptor
    {
        public int Count { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    public WebullConnectionAccessorTests()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(_saves)
            .Options;
        _context = new BudgetTrackerDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    private WebullConnectionAccessor BuildSut() => new(_context, _keyRing);

    private static WebullBalance Balance(string id, decimal value = 100m, string? label = "Individual Cash", decimal? cash = 25m) =>
        new(id, label, "CASH", value, cash, "USD");

    /// <summary>A fetched account with just its balance: no extra totals, no positions.</summary>
    private static WebullAccountPortfolio Only(WebullBalance balance) => new(balance, null, null, null, []);

    /// <summary>Reads the stored row fresh from the database, bypassing the change tracker.</summary>
    private Task<WebullConnection?> StoredAsync(int userId = UserId) =>
        _context.WebullConnections.AsNoTracking().Include(c => c.Accounts).SingleOrDefaultAsync(c => c.UserId == userId);

    // ── GetAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_NothingStored_ReturnsNull()
    {
        Assert.Null(await BuildSut().GetAsync(UserId));
    }

    [Fact]
    public async Task Get_ReturnsSnapshotWithUtcTimesAndBalances_ForThatUserOnly()
    {
        var sut = BuildSut();
        await sut.SaveTokenAsync(UserId, "tok", WebullTokenStatus.Normal, CreatedAt);
        await sut.SavePortfolioAsync(UserId, [Only(Balance("ACC1")), Only(Balance("ACC2", 5m, null, null))], FetchedAt, Today);
        await sut.SetLastErrorAsync(UserId, WebullErrorKind.Unreachable);
        await sut.SetLastErrorAsync(OtherUserId, WebullErrorKind.KeyRejected);
        _context.ChangeTracker.Clear();

        var snapshot = await sut.GetAsync(UserId);

        Assert.NotNull(snapshot);
        Assert.Equal(WebullTokenStatus.Normal, snapshot.TokenStatus);
        Assert.Equal(CreatedAt, snapshot.TokenCreatedAt);
        Assert.Equal(DateTimeKind.Utc, snapshot.TokenCreatedAt!.Value.Kind);
        Assert.Equal(FetchedAt, snapshot.LastFetchedAt);
        Assert.Equal(DateTimeKind.Utc, snapshot.LastFetchedAt!.Value.Kind);
        Assert.Equal(WebullErrorKind.Unreachable, snapshot.LastError);
        Assert.Equal([Balance("ACC1"), Balance("ACC2", 5m, null, null)], snapshot.Balances);
    }

    // ── Tokens ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccessToken_NothingStored_NotFound()
    {
        Assert.Equal(AccessTokenLookup.NotFound, await BuildSut().GetAccessTokenAsync(UserId));
    }

    [Fact]
    public async Task GetAccessToken_RowWithoutToken_NotFound()
    {
        var sut = BuildSut();
        await sut.SetLastErrorAsync(UserId, WebullErrorKind.KeyRejected);

        Assert.Equal(AccessTokenLookup.NotFound, await sut.GetAccessTokenAsync(UserId));
    }

    [Fact]
    public async Task SaveToken_StoresEncrypted_AndRoundTrips()
    {
        var sut = BuildSut();

        await sut.SaveTokenAsync(UserId, "plain-token", WebullTokenStatus.Pending, CreatedAt);

        var stored = await StoredAsync();
        Assert.NotNull(stored);
        Assert.DoesNotContain("plain-token", stored.AccessTokenEncrypted);
        Assert.Equal(AccessTokenLookup.Found("plain-token"), await sut.GetAccessTokenAsync(UserId));
    }

    [Fact]
    public async Task SaveToken_UsesItsOwnPurpose_NotPlaids()
    {
        await BuildSut().SaveTokenAsync(UserId, "plain-token", WebullTokenStatus.Normal, CreatedAt);
        var stored = await StoredAsync();

        var plaidProtector = _keyRing.CreateProtector(PlaidItemAccessor.DataProtectionPurpose);
        var webullProtector = _keyRing.CreateProtector("BudgetTracker.Webull.AccessToken.v1");

        Assert.Equal("BudgetTracker.Webull.AccessToken.v1", WebullConnectionAccessor.DataProtectionPurpose);
        Assert.Equal("plain-token", webullProtector.Unprotect(stored!.AccessTokenEncrypted!));
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => plaidProtector.Unprotect(stored.AccessTokenEncrypted!));
    }

    [Fact]
    public async Task GetAccessToken_KeyRingLost_Undecryptable()
    {
        await new WebullConnectionAccessor(_context, new EphemeralDataProtectionProvider())
            .SaveTokenAsync(UserId, "plain-token", WebullTokenStatus.Normal, CreatedAt);

        Assert.Equal(AccessTokenLookup.Undecryptable, await BuildSut().GetAccessTokenAsync(UserId));
    }

    [Fact]
    public async Task SaveToken_ReplacesTokenSetsAllThreeColumnsClearsError_KeepsBalances()
    {
        var sut = BuildSut();
        await sut.SaveTokenAsync(UserId, "old-token", WebullTokenStatus.Expired, CreatedAt.AddDays(-1));
        await sut.SavePortfolioAsync(UserId, [Only(Balance("ACC1"))], FetchedAt, Today);
        await sut.SetLastErrorAsync(UserId, WebullErrorKind.TokenRejected);

        await sut.SaveTokenAsync(UserId, "new-token", WebullTokenStatus.Pending, CreatedAt);

        var stored = await StoredAsync();
        Assert.Equal(WebullTokenStatus.Pending, stored!.TokenStatus);
        Assert.Equal(CreatedAt, stored.TokenCreatedAt);
        Assert.Null(stored.LastError);
        Assert.Equal(FetchedAt, stored.LastFetchedAt);
        Assert.Single(stored.Accounts);
        Assert.Equal(AccessTokenLookup.Found("new-token"), await sut.GetAccessTokenAsync(UserId));
        Assert.Equal(1, await _context.WebullConnections.CountAsync());
    }

    [Fact]
    public async Task SaveToken_OnErrorOnlyRow_FillsTokenInPlace_ClearsError()
    {
        // A KeyRejected from create-token leaves a row with no token; the next successful create must reuse it.
        var sut = BuildSut();
        await sut.SetLastErrorAsync(UserId, WebullErrorKind.KeyRejected);
        var rowId = (await StoredAsync())!.Id;

        await sut.SaveTokenAsync(UserId, "plain-token", WebullTokenStatus.Pending, CreatedAt);

        var stored = await StoredAsync();
        Assert.Equal(1, await _context.WebullConnections.CountAsync());
        Assert.Equal(rowId, stored!.Id);
        Assert.Null(stored.LastError);
        Assert.Equal(WebullTokenStatus.Pending, stored.TokenStatus);
        Assert.Equal(CreatedAt, stored.TokenCreatedAt);
        Assert.Equal(AccessTokenLookup.Found("plain-token"), await sut.GetAccessTokenAsync(UserId));
    }

    [Fact]
    public async Task SetTokenStatus_UpdatesStatusAndClearsError()
    {
        var sut = BuildSut();
        await sut.SaveTokenAsync(UserId, "tok", WebullTokenStatus.Pending, CreatedAt);
        await sut.SetLastErrorAsync(UserId, WebullErrorKind.Unreachable);

        await sut.SetTokenStatusAsync(UserId, WebullTokenStatus.Normal);

        var stored = await StoredAsync();
        Assert.Equal(WebullTokenStatus.Normal, stored!.TokenStatus);
        Assert.Null(stored.LastError);
        Assert.Equal(CreatedAt, stored.TokenCreatedAt);
    }

    [Fact]
    public async Task SetTokenStatus_NoRow_NoOpAndCreatesNothing()
    {
        await BuildSut().SetTokenStatusAsync(UserId, WebullTokenStatus.Normal);

        Assert.Null(await StoredAsync());
    }

    [Fact]
    public async Task SetTokenStatus_RowWithoutToken_NoOp()
    {
        var sut = BuildSut();
        await sut.SetLastErrorAsync(UserId, WebullErrorKind.KeyRejected);

        await sut.SetTokenStatusAsync(UserId, WebullTokenStatus.Normal);

        var stored = await StoredAsync();
        Assert.Null(stored!.TokenStatus);
        Assert.Equal(WebullErrorKind.KeyRejected, stored.LastError);
    }

    // ── Snapshot merge (SavePortfolioAsync) ─────────────────────────────────

    [Fact]
    public async Task SavePortfolio_NoRow_CreatesOneWithoutToken()
    {
        await BuildSut().SavePortfolioAsync(UserId, [Only(Balance("ACC1"))], FetchedAt, Today);

        var stored = await StoredAsync();
        Assert.NotNull(stored);
        Assert.Null(stored.AccessTokenEncrypted);
        Assert.Equal(FetchedAt, stored.LastFetchedAt);
        Assert.Equal("ACC1", Assert.Single(stored.Accounts).AccountId);
    }

    [Fact]
    public async Task SavePortfolio_RemovesMissingUpdatesMatchingAddsNew_InOneSave_KeepsToken()
    {
        var sut = BuildSut();
        await sut.SaveTokenAsync(UserId, "tok", WebullTokenStatus.Normal, CreatedAt);
        await sut.SavePortfolioAsync(UserId, [Only(Balance("KEEP", 1m)), Only(Balance("DROP", 2m))], FetchedAt.AddHours(-1), Today);
        await sut.SetLastErrorAsync(UserId, WebullErrorKind.Unreachable);
        var keptRowId = (await StoredAsync())!.Accounts.Single(a => a.AccountId == "KEEP").Id;
        _context.ChangeTracker.Clear();
        _saves.Count = 0;

        await sut.SavePortfolioAsync(UserId, [Only(Balance("KEEP", 10m, "Renamed", null)), Only(Balance("NEW", 3m))], FetchedAt, Today);

        Assert.Equal(1, _saves.Count);
        var stored = await StoredAsync();
        Assert.Equal(FetchedAt, stored!.LastFetchedAt);
        Assert.Null(stored.LastError);
        Assert.Equal(WebullTokenStatus.Normal, stored.TokenStatus);
        Assert.Equal(["KEEP", "NEW"], stored.Accounts.Select(a => a.AccountId).Order());
        var kept = stored.Accounts.Single(a => a.AccountId == "KEEP");
        Assert.Equal(keptRowId, kept.Id);
        Assert.Equal(10m, kept.TotalNetLiquidationValue);
        Assert.Equal("Renamed", kept.AccountLabel);
        Assert.Null(kept.TotalCashBalance);
        Assert.Equal(2, await _context.WebullAccounts.CountAsync());
        Assert.Equal(AccessTokenLookup.Found("tok"), await sut.GetAccessTokenAsync(UserId));
    }

    [Fact]
    public async Task SavePortfolio_EmptyFetch_RemovesEveryAccount()
    {
        var sut = BuildSut();
        await sut.SavePortfolioAsync(UserId, [Only(Balance("ACC1")), Only(Balance("ACC2"))], FetchedAt.AddHours(-1), Today);
        _context.ChangeTracker.Clear();

        await sut.SavePortfolioAsync(UserId, [], FetchedAt, Today);

        var stored = await StoredAsync();
        Assert.Empty(stored!.Accounts);
        Assert.Equal(FetchedAt, stored.LastFetchedAt);
    }

    [Fact]
    public async Task SavePortfolio_EmptyFetch_LeavesOtherUsersAccountsAlone()
    {
        var sut = BuildSut();
        await sut.SavePortfolioAsync(OtherUserId, [Only(Balance("ACC1"))], FetchedAt, Today);

        await sut.SavePortfolioAsync(UserId, [], FetchedAt, Today);

        Assert.Single((await StoredAsync(OtherUserId))!.Accounts);
    }

    // ── SetLastErrorAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task SetLastError_NoRow_CreatesOneWithoutToken()
    {
        await BuildSut().SetLastErrorAsync(UserId, WebullErrorKind.KeyRejected);

        var stored = await StoredAsync();
        Assert.NotNull(stored);
        Assert.Equal(WebullErrorKind.KeyRejected, stored.LastError);
        Assert.Null(stored.AccessTokenEncrypted);
        Assert.Null(stored.TokenStatus);
        Assert.Null(stored.TokenCreatedAt);
    }

    [Fact]
    public async Task SetLastError_LeavesTokenStatusAndBalancesUntouched()
    {
        var sut = BuildSut();
        await sut.SaveTokenAsync(UserId, "tok", WebullTokenStatus.Normal, CreatedAt);
        await sut.SavePortfolioAsync(UserId, [Only(Balance("ACC1"))], FetchedAt, Today);

        await sut.SetLastErrorAsync(UserId, WebullErrorKind.TokenRejected);

        var stored = await StoredAsync();
        Assert.Equal(WebullErrorKind.TokenRejected, stored!.LastError);
        Assert.Equal(WebullTokenStatus.Normal, stored.TokenStatus);
        Assert.Equal(FetchedAt, stored.LastFetchedAt);
        Assert.Single(stored.Accounts);
        Assert.Equal(AccessTokenLookup.Found("tok"), await sut.GetAccessTokenAsync(UserId));
    }

    // ── Holdings: SavePortfolioAsync / GetSavedPortfolioAsync / GetUserIdsWithTokenAsync ──

    private static readonly DateOnly Today = new(2026, 10, 22);

    private static WebullPosition Pos(string symbol, decimal quantity = 1m) =>
        new(symbol, "EQUITY", quantity, 10m, 8m, 2m, "USD");

    private static WebullAccountPortfolio Portfolio(string id, decimal value = 100m, params WebullPosition[] positions) =>
        new(Balance(id, value), value - 25m, 1.5m, 3.25m, positions);

    private Task<List<WebullDailyValue>> DailyRowsAsync() =>
        _context.WebullDailyValues.AsNoTracking().OrderBy(d => d.EasternDate).ThenBy(d => d.AccountId).ToListAsync();

    [Fact]
    public async Task SavePortfolio_EmptyDb_WritesEverythingUnderTheNewConnection_InOneSave()
    {
        _saves.Count = 0;

        await BuildSut().SavePortfolioAsync(UserId, [Portfolio("ACC1", 100m, Pos("AAPL"), Pos("MSFT"))], FetchedAt, Today);

        Assert.Equal(1, _saves.Count);
        var stored = await StoredAsync();
        Assert.NotNull(stored);
        Assert.Equal(FetchedAt, stored.LastFetchedAt);
        Assert.Equal(2, await _context.WebullPositions.CountAsync());
        var daily = Assert.Single(await DailyRowsAsync());
        // InMemory does not enforce FKs, so check the id flowed in rather than defaulting to 0.
        Assert.Equal(stored.Id, daily.WebullConnectionId);
        Assert.NotEqual(0, daily.WebullConnectionId);
    }

    [Fact]
    public async Task SavePortfolio_WritesTotalsAndDailyValueFromTheFetch()
    {
        await BuildSut().SavePortfolioAsync(UserId, [new WebullAccountPortfolio(Balance("ACC1", 100m, cash: null), 75m, -1.5m, null, [])], FetchedAt, Today);

        var account = Assert.Single((await StoredAsync())!.Accounts);
        Assert.Equal((75m, -1.5m, (decimal?)null), (account.TotalMarketValue, account.TotalDayProfitLoss, account.TotalUnrealizedProfitLoss));
        var daily = Assert.Single(await DailyRowsAsync());
        Assert.Equal(
            (Today, "ACC1", 100m, (decimal?)null, (decimal?)75m, (decimal?)-1.5m, (decimal?)null, "USD", FetchedAt),
            (daily.EasternDate, daily.AccountId, daily.TotalValue, daily.Cash, daily.MarketValue, daily.DayProfitLoss, daily.UnrealizedProfitLoss, daily.Currency, daily.FetchedAt));
    }

    [Fact]
    public async Task SavePortfolio_ReplacesPositions_DoesNotAppend()
    {
        var sut = BuildSut();
        await sut.SavePortfolioAsync(UserId, [Portfolio("ACC1", 100m, Pos("AAPL"), Pos("MSFT"))], FetchedAt.AddHours(-1), Today);
        _context.ChangeTracker.Clear(); // positions must come from the database (ThenInclude), not the tracker

        await sut.SavePortfolioAsync(UserId, [Portfolio("ACC1", 100m, Pos("NVDA", 3m))], FetchedAt, Today);

        var position = Assert.Single(await _context.WebullPositions.AsNoTracking().ToListAsync());
        Assert.Equal(("NVDA", 3m), (position.Symbol, position.Quantity));
    }

    [Fact]
    public async Task SavePortfolio_EmptyPositions_LeavesNone()
    {
        var sut = BuildSut();
        await sut.SavePortfolioAsync(UserId, [Portfolio("ACC1", 100m, Pos("AAPL"))], FetchedAt.AddHours(-1), Today);
        _context.ChangeTracker.Clear(); // positions must come from the database (ThenInclude), not the tracker

        await sut.SavePortfolioAsync(UserId, [Portfolio("ACC1")], FetchedAt, Today);

        Assert.Empty(await _context.WebullPositions.ToListAsync());
        Assert.Single((await StoredAsync())!.Accounts);
    }

    [Fact]
    public async Task SavePortfolio_DroppedAccount_RemovesItsPositions_KeepsItsDailyRows()
    {
        var sut = BuildSut();
        await sut.SavePortfolioAsync(UserId, [Portfolio("KEEP", 1m, Pos("A")), Portfolio("DROP", 2m, Pos("B"), Pos("C"))], FetchedAt.AddDays(-1), Today.AddDays(-1));
        _context.ChangeTracker.Clear(); // positions must come from the database (ThenInclude), not the tracker

        await sut.SavePortfolioAsync(UserId, [Portfolio("KEEP", 1m, Pos("A"))], FetchedAt, Today);

        Assert.Equal(["A"], await _context.WebullPositions.Select(p => p.Symbol).ToListAsync());
        var daily = await DailyRowsAsync();
        Assert.Contains(daily, d => d.AccountId == "DROP" && d.EasternDate == Today.AddDays(-1));
        Assert.DoesNotContain(daily, d => d.AccountId == "DROP" && d.EasternDate == Today);
    }

    [Fact]
    public async Task SavePortfolio_SameDay_UpdatesTheExistingDailyRow()
    {
        var sut = BuildSut();
        await sut.SavePortfolioAsync(UserId, [Portfolio("ACC1", 100m)], FetchedAt.AddHours(-2), Today);
        _context.ChangeTracker.Clear();

        await sut.SavePortfolioAsync(UserId, [Portfolio("ACC1", 250m)], FetchedAt, Today);

        var daily = Assert.Single(await DailyRowsAsync());
        Assert.Equal(250m, daily.TotalValue);
        Assert.Equal(225m, daily.MarketValue);
        Assert.Equal(25m, daily.Cash);
        Assert.Equal(1.5m, daily.DayProfitLoss);
        Assert.Equal(3.25m, daily.UnrealizedProfitLoss);
        Assert.Equal("USD", daily.Currency);
        Assert.Equal(FetchedAt, daily.FetchedAt);
    }

    [Fact]
    public async Task SavePortfolio_LeavesOtherDatesAndOtherUsersUntouched()
    {
        var sut = BuildSut();
        await sut.SavePortfolioAsync(UserId, [Portfolio("ACC1", 100m)], FetchedAt.AddDays(-1), Today.AddDays(-1));
        await sut.SavePortfolioAsync(OtherUserId, [Portfolio("ACC1", 7m)], FetchedAt, Today);

        await sut.SavePortfolioAsync(UserId, [Portfolio("ACC1", 200m)], FetchedAt, Today);

        var myId = (await StoredAsync())!.Id;
        var theirId = (await StoredAsync(OtherUserId))!.Id;
        var daily = await DailyRowsAsync();
        Assert.Equal([(Today.AddDays(-1), 100m), (Today, 200m)], daily.Where(d => d.WebullConnectionId == myId).Select(d => (d.EasternDate, d.TotalValue)));
        var theirs = daily.Single(d => d.WebullConnectionId == theirId);
        Assert.Equal(7m, theirs.TotalValue);
    }

    [Fact]
    public async Task SavePortfolio_DuplicateAccountId_ThrowsBeforeWritingAnything()
    {
        var sut = BuildSut();
        await sut.SavePortfolioAsync(UserId, [Portfolio("ACC1", 1m, Pos("A"))], FetchedAt.AddHours(-1), Today);
        _context.ChangeTracker.Clear();
        _saves.Count = 0;

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.SavePortfolioAsync(UserId, [Portfolio("ACC1", 2m), Portfolio("ACC1", 3m)], FetchedAt, Today));

        Assert.Equal(0, _saves.Count);
        Assert.Equal(1m, Assert.Single((await StoredAsync())!.Accounts).TotalNetLiquidationValue);
        Assert.Single(await _context.WebullPositions.ToListAsync());
    }

    [Fact]
    public async Task GetSavedPortfolio_NothingSaved_Empty()
    {
        Assert.Empty(await BuildSut().GetSavedPortfolioAsync(UserId));
    }

    [Fact]
    public async Task GetSavedPortfolio_RoundTripsTheLastSave_ForThatUserOnly_InBalancesOrder()
    {
        var sut = BuildSut();
        var saved = new[]
        {
            Portfolio("ACC1", 100m, Pos("AAPL", 2m), Pos("BTC", 0.0123456789m)),
            new WebullAccountPortfolio(Balance("ACC2", 5m, null, null), null, null, null, [])
        };
        await sut.SavePortfolioAsync(UserId, saved, FetchedAt, Today);
        await sut.SavePortfolioAsync(OtherUserId, [Portfolio("OTHER")], FetchedAt, Today);
        _context.ChangeTracker.Clear();

        var portfolio = await sut.GetSavedPortfolioAsync(UserId);

        Assert.Equal(["ACC1", "ACC2"], portfolio.Select(p => p.Balance.AccountId));
        Assert.Equal((await sut.GetAsync(UserId))!.Balances, portfolio.Select(p => p.Balance));
        Assert.Equal(saved[0].Positions, portfolio[0].Positions);
        Assert.Equal((75m, 1.5m, (decimal?)3.25m), (portfolio[0].TotalMarketValue, portfolio[0].TotalDayProfitLoss, portfolio[0].TotalUnrealizedProfitLoss));
        Assert.Equal(saved[1] with { Positions = [] }, portfolio[1] with { Positions = [] });
        Assert.Empty(portfolio[1].Positions);
    }

    [Fact]
    public async Task GetUserIdsWithToken_EveryUserWithAStoredToken_InAnyStatus()
    {
        var sut = BuildSut();
        await sut.SaveTokenAsync(UserId, "tok", WebullTokenStatus.Pending, CreatedAt);
        await sut.SaveTokenAsync(7, "tok", WebullTokenStatus.Invalid, CreatedAt);
        await sut.SetLastErrorAsync(OtherUserId, WebullErrorKind.KeyRejected); // row, but no token

        Assert.Equal([UserId, 7], (await sut.GetUserIdsWithTokenAsync()).Order());
    }

    [Fact]
    public async Task SavePortfolio_AccountDroppedLaterSameDay_ItsRowForTodayIsUntouched()
    {
        var sut = BuildSut();
        await sut.SavePortfolioAsync(UserId, [Portfolio("KEEP", 1m), Portfolio("DROP", 2m)], FetchedAt.AddHours(-2), Today);
        _context.ChangeTracker.Clear();

        await sut.SavePortfolioAsync(UserId, [Portfolio("KEEP", 10m)], FetchedAt, Today);

        var dropped = (await DailyRowsAsync()).Single(d => d.AccountId == "DROP");
        Assert.Equal((2m, FetchedAt.AddHours(-2)), (dropped.TotalValue, dropped.FetchedAt));
        Assert.Equal(10m, (await DailyRowsAsync()).Single(d => d.AccountId == "KEEP").TotalValue);
    }

    [Fact]
    public async Task SavePortfolio_SameDay_LaterFetchWithNullTotals_OverwritesWithNull()
    {
        var sut = BuildSut();
        await sut.SavePortfolioAsync(UserId, [Portfolio("ACC1", 100m)], FetchedAt.AddHours(-2), Today);
        _context.ChangeTracker.Clear();

        await sut.SavePortfolioAsync(UserId, [new WebullAccountPortfolio(Balance("ACC1", 90m, cash: null), null, null, null, [])], FetchedAt, Today);

        var daily = Assert.Single(await DailyRowsAsync());
        Assert.Equal(90m, daily.TotalValue);
        Assert.Null(daily.Cash);
        Assert.Null(daily.MarketValue);
        Assert.Null(daily.DayProfitLoss);
        Assert.Null(daily.UnrealizedProfitLoss);
    }

    [Fact]
    public async Task SetLastError_AfterSavePortfolio_KeepsPositionsDailyRowsAndFetchTime()
    {
        var sut = BuildSut();
        await sut.SaveTokenAsync(UserId, "tok", WebullTokenStatus.Normal, CreatedAt);
        await sut.SavePortfolioAsync(UserId, [Portfolio("ACC1", 100m, Pos("AAPL"), Pos("MSFT"))], FetchedAt, Today);
        _context.ChangeTracker.Clear();

        await sut.SetLastErrorAsync(UserId, WebullErrorKind.Unreachable);

        var stored = await StoredAsync();
        Assert.Equal(WebullErrorKind.Unreachable, stored!.LastError);
        Assert.Equal(FetchedAt, stored.LastFetchedAt);
        Assert.Equal(2, await _context.WebullPositions.CountAsync());
        Assert.Single(await DailyRowsAsync());
    }
}
