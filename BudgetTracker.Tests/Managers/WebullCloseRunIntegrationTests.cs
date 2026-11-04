using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Webull;
using BudgetTracker.Server.Managers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Tests.Managers;

/// <summary>
/// The close run walks every user through one scoped DbContext. These tests use the real connection accessor and engines
/// to prove one user's failed save cannot leak its pending changes into, or poison, the next user's save.
/// </summary>
public class WebullCloseRunIntegrationTests : IDisposable
{
    private const int FailingUser = 5;
    private const int HealthyUser = 6;

    private readonly FailingDailyValueSave _interceptor = new();
    private readonly BudgetTrackerDbContext _context;
    private readonly WebullConnectionAccessor _connections;

    /// <summary>Throws on a save while an Added daily row for <see cref="TargetConnectionId"/> is pending.</summary>
    private sealed class FailingDailyValueSave : SaveChangesInterceptor
    {
        public int? TargetConnectionId { get; set; }
        public bool FailOnlyOnce { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var tracker = eventData.Context!.ChangeTracker;
            tracker.DetectChanges(); // rows added through a navigation are only tracked after this
            if (TargetConnectionId is { } target &&
                tracker.Entries<WebullDailyValue>().Any(e => e.State == EntityState.Added && e.Entity.WebullConnectionId == target))
            {
                if (FailOnlyOnce)
                    TargetConnectionId = null;
                throw new DbUpdateException("duplicate key value violates unique constraint \"PK_WebullDailyValues\"");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    /// <summary>Returns one account with one position per token.</summary>
    private sealed class FakeWebull : IWebullAccessor
    {
        public Task<IReadOnlyList<WebullAccountPortfolio>> GetPortfolioAsync(string accessToken, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WebullAccountPortfolio>>(
            [
                new(new WebullBalance($"ACC-{accessToken}", "Individual Margin", "MARGIN", 1000m, 100m, "USD"), 900m, 5m, 50m,
                    [new WebullPosition("VTI", "EQUITY", 3m, 300m, 280m, 60m, "USD")])
            ]);

        public Task<WebullToken> CreateTokenAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("never");
        public Task<WebullTokenStatus> CheckTokenAsync(string accessToken, CancellationToken cancellationToken = default) => throw new InvalidOperationException("never");
    }

    public WebullCloseRunIntegrationTests()
    {
        _context = new BudgetTrackerDbContext(new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(_interceptor)
            .Options);
        _connections = new WebullConnectionAccessor(_context, new EphemeralDataProtectionProvider());
    }

    public void Dispose() => _context.Dispose();

    private async Task SeedTwoDueUsersAsync()
    {
        var yesterday = DateTime.UtcNow.AddDays(-1);
        await _connections.SaveTokenAsync(FailingUser, $"tok-{FailingUser}", WebullTokenStatus.Normal, yesterday);
        await _connections.SaveTokenAsync(HealthyUser, $"tok-{HealthyUser}", WebullTokenStatus.Normal, yesterday);
        _context.ChangeTracker.Clear();
        _interceptor.TargetConnectionId = await ConnectionIdAsync(FailingUser);
    }

    private Task<int> ConnectionIdAsync(int userId) =>
        _context.WebullConnections.Where(c => c.UserId == userId).Select(c => c.Id).SingleAsync();

    private Task RunAsync() =>
        new WebullManager(
                new WebullEngine(),
                new WebullHoldingsEngine(),
                new FakeWebull(),
                _connections,
                Options.Create(new WebullOptions { AppKey = "key", AppSecret = "secret" }),
                NullLogger<WebullManager>.Instance)
            .RunCloseSnapshotsAsync();

    private async Task<(DateTime? LastFetchedAt, int DailyRows, int Positions)> PersistedAsync(int userId)
    {
        var id = await ConnectionIdAsync(userId);
        var connection = await _context.WebullConnections.AsNoTracking().SingleAsync(c => c.Id == id);
        var dailyRows = await _context.WebullDailyValues.AsNoTracking().CountAsync(d => d.WebullConnectionId == id);
        var positions = await _context.WebullPositions.AsNoTracking()
            .CountAsync(p => _context.WebullAccounts.Any(a => a.Id == p.WebullAccountId && a.WebullConnectionId == id));
        return (connection.LastFetchedAt, dailyRows, positions);
    }

    [Fact]
    public async Task RunCloseSnapshots_OneUsersSaveFails_ItsFetchIsNotPersistedByTheNextUsersSave()
    {
        await SeedTwoDueUsersAsync();
        _interceptor.FailOnlyOnce = true; // a transient failure: the next save on the context would succeed

        await RunAsync();
        _context.ChangeTracker.Clear();

        Assert.Equal((null, 0, 0), await PersistedAsync(FailingUser));
        var healthy = await PersistedAsync(HealthyUser);
        Assert.NotNull(healthy.LastFetchedAt);
        Assert.Equal(1, healthy.DailyRows);
    }

    [Fact]
    public async Task RunCloseSnapshots_DailyRowKeyRaceForOneUser_NextUserStillSaves()
    {
        await SeedTwoDueUsersAsync(); // the interceptor keeps failing while user 5's daily row is pending, like a real PK conflict

        await RunAsync();
        _context.ChangeTracker.Clear();

        var healthy = await PersistedAsync(HealthyUser);
        Assert.NotNull(healthy.LastFetchedAt);
        Assert.Equal(1, healthy.DailyRows);
    }
}
