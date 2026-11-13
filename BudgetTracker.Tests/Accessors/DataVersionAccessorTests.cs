using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BudgetTracker.Tests.Accessors;

/// <summary>
/// <see cref="DataVersionAccessor"/> against a real relational provider (EF Core SQLite, in memory), so the bump's
/// <c>ExecuteUpdate</c> is translated and executed as an actual <c>UPDATE ... SET "DataVersion" = "DataVersion" + 1</c>.
/// The full <see cref="BudgetTrackerDbContext"/> model is used, but <c>EnsureCreated</c> cannot run on SQLite (the
/// Postgres-only default <c>now() AT TIME ZONE 'utc'</c> is a syntax error there), so only the <c>Users</c> table is
/// created by hand. The SQL text is SQLite's, not Npgsql's; the LINQ and the server-side increment are the same.
/// </summary>
public sealed class DataVersionAccessorTests : IDisposable
{
    private static readonly DateTime CreatedAt = new(2026, 1, 2, 3, 4, 5);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly BudgetTrackerDbContext _context;
    private readonly Mock<ILogger<DataVersionAccessor>> _logger = new();

    public DataVersionAccessorTests()
    {
        _connection.Open();
        Execute("""
            CREATE TABLE "Users" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "CognitoSub" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "DataVersion" INTEGER NOT NULL
            );
            """);
        _context = new BudgetTrackerDbContext(new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseSqlite(_connection)
            .Options);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private DataVersionAccessor BuildSut() => new(_context, _logger.Object);

    private async Task<User> SeedUserAsync(string sub, long version)
    {
        var user = new User { CognitoSub = sub, CreatedAt = CreatedAt, DataVersion = version };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return user;
    }

    private async Task<User> ReloadAsync(int id) => await _context.Users.AsNoTracking().SingleAsync(u => u.Id == id);

    // ── BumpAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Bump_ExistingUser_IncrementsByOneAndReturnsTrue()
    {
        var user = await SeedUserAsync("sub-1", 41);

        var bumped = await BuildSut().BumpAsync(user.Id);

        Assert.True(bumped);
        Assert.Equal(42L, (await ReloadAsync(user.Id)).DataVersion);
    }

    [Fact]
    public async Task Bump_UnknownUser_ReturnsFalse_AndChangesNoRow()
    {
        var user = await SeedUserAsync("sub-1", 7);

        var bumped = await BuildSut().BumpAsync(user.Id + 1000);

        Assert.False(bumped);
        Assert.Equal(7L, (await ReloadAsync(user.Id)).DataVersion);
    }

    [Fact]
    public async Task Bump_CalledRepeatedly_EachCallAddsOne()
    {
        var user = await SeedUserAsync("sub-1", 0);
        var sut = BuildSut();

        for (var i = 0; i < 3; i++)
            Assert.True(await sut.BumpAsync(user.Id));

        Assert.Equal(3L, (await ReloadAsync(user.Id)).DataVersion);
    }

    [Fact]
    public async Task Bump_ChangesOnlyThatUsersDataVersion()
    {
        var user = await SeedUserAsync("sub-1", 5);
        var other = await SeedUserAsync("sub-2", 9);

        await BuildSut().BumpAsync(user.Id);

        var reloaded = await ReloadAsync(user.Id);
        Assert.Equal(6L, reloaded.DataVersion);
        Assert.Equal("sub-1", reloaded.CognitoSub);
        Assert.Equal(CreatedAt, reloaded.CreatedAt);
        var untouched = await ReloadAsync(other.Id);
        Assert.Equal(9L, untouched.DataVersion);
        Assert.Equal("sub-2", untouched.CognitoSub);
        Assert.Equal(CreatedAt, untouched.CreatedAt);
    }

    [Fact]
    public async Task Bump_IncrementsInTheDatabase_WithoutLoadingOrTrackingTheUser()
    {
        var user = await SeedUserAsync("sub-1", 5);

        await BuildSut().BumpAsync(user.Id);

        // A load-and-save bump would leave a tracked User behind; the increment proves the bump actually ran.
        Assert.Empty(_context.ChangeTracker.Entries());
        Assert.Equal(6L, (await ReloadAsync(user.Id)).DataVersion);
    }

    [Fact]
    public async Task Bump_DatabaseRejectsTheUpdate_ReturnsFalseAndLogsWarningWithoutThrowing()
    {
        var user = await SeedUserAsync("sub-1", 5);
        Execute("""DROP TABLE "Users";""");

        var bumped = await BuildSut().BumpAsync(user.Id);

        Assert.False(bumped);
        _logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(user.Id.ToString())),
            It.IsAny<SqliteException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    // ── GetByCognitoSubAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetByCognitoSub_ReturnsTheUsersVersion()
    {
        await SeedUserAsync("other", 9);
        await SeedUserAsync("sub-1", 42);

        var version = await BuildSut().GetByCognitoSubAsync("sub-1");

        Assert.Equal(42L, version);
    }

    [Fact]
    public async Task GetByCognitoSub_UnknownSub_ReturnsNull()
    {
        await SeedUserAsync("sub-1", 42);

        var version = await BuildSut().GetByCognitoSubAsync("nobody");

        Assert.Null(version);
    }

    [Fact]
    public async Task GetByCognitoSub_ReflectsAPriorBump()
    {
        var user = await SeedUserAsync("sub-1", 42);
        var sut = BuildSut();

        await sut.BumpAsync(user.Id);

        Assert.Equal(43L, await sut.GetByCognitoSubAsync("sub-1"));
    }

    [Fact]
    public async Task GetByCognitoSub_DoesNotTrackTheUser()
    {
        await SeedUserAsync("sub-1", 1);

        await BuildSut().GetByCognitoSubAsync("sub-1");

        Assert.Empty(_context.ChangeTracker.Entries());
    }
}
