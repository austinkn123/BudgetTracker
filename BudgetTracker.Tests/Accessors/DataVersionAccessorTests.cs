using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BudgetTracker.Tests.Accessors;

/// <summary>
/// Read path and the never-throw contract of <see cref="DataVersionAccessor"/>. The bump itself is an
/// <c>ExecuteUpdate</c>, which the InMemory provider cannot translate; that limitation is used here as a realistic
/// provider failure. The atomic increment is verified against real Postgres outside this suite (see the PR notes).
/// </summary>
public class DataVersionAccessorTests : IDisposable
{
    private readonly BudgetTrackerDbContext _context;
    private readonly Mock<ILogger<DataVersionAccessor>> _logger = new();

    public DataVersionAccessorTests()
    {
        _context = new BudgetTrackerDbContext(new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    public void Dispose() => _context.Dispose();

    private DataVersionAccessor BuildSut() => new(_context, _logger.Object);

    private async Task<User> SeedUserAsync(string sub, long version)
    {
        var user = new User { CognitoSub = sub, DataVersion = version };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return user;
    }

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
    public async Task GetByCognitoSub_DoesNotTrackTheUser()
    {
        await SeedUserAsync("sub-1", 1);

        await BuildSut().GetByCognitoSubAsync("sub-1");

        Assert.Empty(_context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Bump_ProviderFails_ReturnsFalseAndLogsWarningWithoutThrowing()
    {
        var user = await SeedUserAsync("sub-1", 5);

        var bumped = await BuildSut().BumpAsync(user.Id);

        Assert.False(bumped);
        _logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(user.Id.ToString())),
            It.IsNotNull<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Bump_NeverLoadsOrTracksTheUserEntity()
    {
        var user = await SeedUserAsync("sub-1", 5);

        await BuildSut().BumpAsync(user.Id);

        Assert.Empty(_context.ChangeTracker.Entries());
        Assert.Equal(5L, (await _context.Users.AsNoTracking().SingleAsync()).DataVersion);
    }
}
