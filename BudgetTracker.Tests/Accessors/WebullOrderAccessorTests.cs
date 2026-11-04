using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Webull;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BudgetTracker.Tests.Accessors;

/// <summary>
/// Tests for <see cref="WebullOrderAccessor"/> against the InMemory provider: insert-or-overwrite per order, the sync row
/// saved with its page, UTC kinds on read, all-or-nothing saves, and history surviving a balance snapshot that drops an
/// account. Check constraints are not enforced by InMemory; they were verified against Postgres when the migration was applied.
/// </summary>
public class WebullOrderAccessorTests : IDisposable
{
    private const int UserId = 5;
    private const int OtherUserId = 6;
    private static readonly DateTime Placed = new(2026, 3, 2, 14, 30, 0, 123, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 10, 25, 15, 0, 0, DateTimeKind.Utc);

    private readonly BudgetTrackerDbContext _context;
    private readonly FailingSave _failingSave = new();
    private readonly EphemeralDataProtectionProvider _keyRing = new();

    /// <summary>Makes the next SaveChanges throw while <see cref="Fail"/> is set.</summary>
    private sealed class FailingSave : SaveChangesInterceptor
    {
        public bool Fail { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Fail ? throw new DbUpdateException("simulated failure") : base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public WebullOrderAccessorTests()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(_failingSave)
            .Options;
        _context = new BudgetTrackerDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    private WebullOrderAccessor BuildSut() => new(_context);

    private async Task<int> ConnectAsync(int userId = UserId)
    {
        await new WebullConnectionAccessor(_context, _keyRing).SaveTokenAsync(userId, "tok", WebullTokenStatus.Normal, Now);
        _context.ChangeTracker.Clear();
        return await _context.WebullConnections.Where(c => c.UserId == userId).Select(c => c.Id).SingleAsync();
    }

    private static WebullOrder Order(string id = "O1", string status = "FILLED", string account = "ACC1", decimal? quantity = 10m) =>
        new(account, id, "AAPL", "EQUITY", "BUY", status, quantity, 150.25m, Placed, status == "FILLED" ? Placed.AddSeconds(1) : null);

    private static WebullOrderSync Sync(string account = "ACC1", WebullOrderWindow? window = null) =>
        new(account, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Now, window, null);

    private async Task SaveAsync(WebullOrderSync sync, params WebullOrder[] orders)
    {
        await BuildSut().SavePageAsync(UserId, sync, orders);
        _context.ChangeTracker.Clear();
    }

    // ── SavePageAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SavePage_InsertsOrdersAndSync_InOneRoundTrip()
    {
        await ConnectAsync();
        var window = new WebullOrderWindow(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc), "pk=2");

        await SaveAsync(Sync(window: window), Order("O1"), Order("O2", "CANCELLED"));

        var sut = BuildSut();
        Assert.Equal([Order("O1"), Order("O2", "CANCELLED")], await sut.GetOrdersAsync(UserId));
        Assert.Equal([Sync(window: window)], await sut.GetSyncsAsync(UserId));
    }

    [Fact]
    public async Task SavePage_OverlappingRefetch_OverwritesTheSavedOrder()
    {
        await ConnectAsync();
        await SaveAsync(Sync(), Order("O1", "PENDING", quantity: null));

        await SaveAsync(Sync(), Order("O1", "FILLED"));

        Assert.Equal(Order("O1", "FILLED"), Assert.Single(await BuildSut().GetOrdersAsync(UserId)));
        Assert.Equal(1, await _context.WebullOrders.CountAsync());
    }

    [Fact]
    public async Task SavePage_DuplicateWithinOnePage_CollapsesAndTheLastWins()
    {
        await ConnectAsync();

        await SaveAsync(Sync(), Order("O1", "PENDING"), Order("O2"), Order("O1", "PARTIAL_FILLED", quantity: 4m));

        Assert.Equal([Order("O1", "PARTIAL_FILLED", quantity: 4m), Order("O2")], await BuildSut().GetOrdersAsync(UserId));
    }

    [Fact]
    public async Task SavePage_EmptyPage_SavesOnlyTheProgress()
    {
        await ConnectAsync();
        await SaveAsync(Sync(), Order("O1"));
        var failed = Sync(window: new WebullOrderWindow(Now.AddDays(-7), Now, null)) with { LastError = WebullErrorKind.Unreachable };

        await SaveAsync(failed);

        var sut = BuildSut();
        Assert.Equal([failed], await sut.GetSyncsAsync(UserId));
        Assert.Equal([Order("O1")], await sut.GetOrdersAsync(UserId));
    }

    [Fact]
    public async Task SavePage_SyncRowIsOverwritten_WindowClearedWhenNone()
    {
        await ConnectAsync();
        await SaveAsync(Sync(window: new WebullOrderWindow(Now.AddDays(-7), Now, "pk")));

        var completed = Sync() with { CoveredFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        await SaveAsync(completed);

        Assert.Equal([completed], await BuildSut().GetSyncsAsync(UserId));
        var row = await _context.WebullOrderSyncs.SingleAsync();
        Assert.Equal((null, null, null), (row.WindowStart, row.WindowEnd, row.PaginationKey));
    }

    [Fact]
    public async Task SavePage_OrderForAnotherAccount_ThrowsAndSavesNothing()
    {
        await ConnectAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => BuildSut().SavePageAsync(UserId, Sync("ACC1"), [Order("O1"), Order("O2", account: "ACC2")]));

        _context.ChangeTracker.Clear();
        Assert.Empty(await _context.WebullOrders.ToListAsync());
        Assert.Empty(await _context.WebullOrderSyncs.ToListAsync());
    }

    [Fact]
    public async Task SavePage_NoConnection_ThrowsInvalidOperation()
    {
        await ConnectAsync(OtherUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildSut().SavePageAsync(UserId, Sync(), [Order()]));

        _context.ChangeTracker.Clear();
        Assert.Empty(await _context.WebullOrders.ToListAsync());
        Assert.Empty(await _context.WebullOrderSyncs.ToListAsync());
    }

    [Fact]
    public async Task SavePage_FailedSave_LeavesNothingTracked_AndRethrows()
    {
        await ConnectAsync();
        await SaveAsync(Sync(), Order("O1", "PENDING"));
        _failingSave.Fail = true;

        await Assert.ThrowsAsync<DbUpdateException>(() => BuildSut().SavePageAsync(UserId, Sync() with { CoveredTo = Now.AddHours(1) }, [Order("O1"), Order("O2")]));

        Assert.Empty(_context.ChangeTracker.Entries());
        _failingSave.Fail = false;
        var sut = BuildSut();
        Assert.Equal([Order("O1", "PENDING")], await sut.GetOrdersAsync(UserId));
        Assert.Equal([Sync()], await sut.GetSyncsAsync(UserId));
    }

    // ── Reads ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reads_NoConnection_AreEmpty()
    {
        var sut = BuildSut();

        Assert.Empty(await sut.GetOrdersAsync(UserId));
        Assert.Empty(await sut.GetSyncsAsync(UserId));
    }

    [Fact]
    public async Task Reads_AreScopedToTheUser_AndOrderedByAccountThenOrderId()
    {
        await ConnectAsync();
        await ConnectAsync(OtherUserId);
        await BuildSut().SavePageAsync(OtherUserId, Sync("ACC1"), [Order("X1")]);
        await SaveAsync(Sync("ACC2"), Order("O4", account: "ACC2"), Order("O3", account: "ACC2"));
        await SaveAsync(Sync("ACC1"), Order("O2"), Order("O1"));

        var sut = BuildSut();
        Assert.Equal(["ACC1/O1", "ACC1/O2", "ACC2/O3", "ACC2/O4"], (await sut.GetOrdersAsync(UserId)).Select(o => $"{o.AccountId}/{o.OrderId}"));
        Assert.Equal(["ACC1", "ACC2"], (await sut.GetSyncsAsync(UserId)).Select(s => s.AccountId));
    }

    [Fact]
    public async Task Reads_ReturnEveryTimestampWithKindUtc()
    {
        // Npgsql reads timestamp without time zone back as Unspecified; seed it that way.
        var connectionId = await ConnectAsync();
        static DateTime Unspecified(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        _context.WebullOrders.Add(new WebullAccountOrder
        {
            WebullConnectionId = connectionId, AccountId = "ACC1", OrderId = "O1", Symbol = "AAPL", Side = "BUY", Status = "FILLED",
            PlacedAt = Unspecified(Placed), FilledAt = Unspecified(Placed.AddSeconds(1))
        });
        _context.WebullOrderSyncs.Add(new WebullAccountOrderSync
        {
            WebullConnectionId = connectionId, AccountId = "ACC1",
            CoveredFrom = Unspecified(Placed), CoveredTo = Unspecified(Now), WindowStart = Unspecified(Placed), WindowEnd = Unspecified(Now)
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var order = Assert.Single(await BuildSut().GetOrdersAsync(UserId));
        var sync = Assert.Single(await BuildSut().GetSyncsAsync(UserId));

        Assert.All(
            [order.PlacedAt, order.FilledAt!.Value, sync.CoveredFrom!.Value, sync.CoveredTo!.Value, sync.Window!.Start, sync.Window.End],
            t => Assert.Equal(DateTimeKind.Utc, t.Kind));
        Assert.Equal((Placed, Now), (sync.Window.Start, sync.Window.End));
    }

    // ── Separation from the balance snapshot ────────────────────────────────

    [Fact]
    public async Task BalanceSnapshotDroppingAnAccount_LeavesItsOrdersAndSyncIntact()
    {
        await ConnectAsync();
        var portfolio = new WebullConnectionAccessor(_context, _keyRing);
        WebullAccountPortfolio Account(string id) => new(new WebullBalance(id, null, "CASH", 1m, null, "USD"), null, null, null, []);
        await portfolio.SavePortfolioAsync(UserId, [Account("ACC1"), Account("ACC2")], Now, new DateOnly(2026, 10, 25));
        _context.ChangeTracker.Clear();
        await SaveAsync(Sync("ACC2"), Order("O1", account: "ACC2"));

        await portfolio.SavePortfolioAsync(UserId, [Account("ACC1")], Now, new DateOnly(2026, 10, 25));
        _context.ChangeTracker.Clear();

        Assert.Equal(["ACC1"], await _context.WebullAccounts.Select(a => a.AccountId).ToListAsync());
        Assert.Equal([Order("O1", account: "ACC2")], await BuildSut().GetOrdersAsync(UserId));
        Assert.Equal([Sync("ACC2")], await BuildSut().GetSyncsAsync(UserId));
    }
}
