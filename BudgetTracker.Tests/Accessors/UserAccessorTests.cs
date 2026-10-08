using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BudgetTracker.Tests.Accessors;

/// <summary>
/// First-login provisioning races: the dashboard fires several requests at once, each finds no user and tries to insert.
/// </summary>
public class UserAccessorTests : IDisposable
{
    private const string SubIndex = "IX_Users_CognitoSub";
    private const string Sub = "cognito-sub-1";
    private readonly FailingSaveInterceptor _interceptor = new();
    private readonly BudgetTrackerDbContext _context;

    public UserAccessorTests()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(_interceptor)
            .Options;
        _context = new BudgetTrackerDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    private UserAccessor BuildSut() => new(_context);

    [Fact]
    public async Task Create_LosesRaceOnCognitoSub_ReturnsWinningUsersId()
    {
        var winner = new User { CognitoSub = Sub };
        _context.Users.Add(winner);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        _interceptor.FailNextSaveWith(PostgresErrorCodes.UniqueViolation, SubIndex);

        var id = await BuildSut().CreateAsync(new User { CognitoSub = Sub });

        Assert.Equal(winner.Id, id);
        Assert.DoesNotContain(_context.ChangeTracker.Entries<User>(), e => e.State == EntityState.Added);
    }

    [Fact]
    public async Task Create_OtherUniqueIndexViolated_RethrowsOriginalException()
    {
        _interceptor.FailNextSaveWith(PostgresErrorCodes.UniqueViolation, "PK_Users");

        await Assert.ThrowsAsync<DbUpdateException>(() => BuildSut().CreateAsync(new User { CognitoSub = Sub }));
    }
}
