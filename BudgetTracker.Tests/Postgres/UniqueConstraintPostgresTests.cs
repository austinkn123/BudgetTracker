using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// Accessor behaviour that hangs on a real unique-index violation (SqlState 23505 and the index's name), which neither
/// the InMemory provider nor SQLite reports the way Npgsql does.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Postgres")]
public sealed class UniqueConstraintPostgresTests(PostgresFixture fixture)
{
    private const int ParallelLogins = 8;

    private async Task<int> CreateUserAsync(string sub)
    {
        await using var context = fixture.CreateContext();
        return await new UserAccessor(context).CreateAsync(new User { CognitoSub = sub });
    }

    private async Task<int> CreateCategoryAsync(int userId, string name)
    {
        await using var context = fixture.CreateContext();
        return await new CategoryAccessor(context).CreateAsync(new Category { UserId = userId, Name = name });
    }

    [PostgresFact]
    public async Task Should_ReturnTheExistingUser_When_CognitoSubWasAlreadyProvisioned()
    {
        var sub = $"sub-{Guid.NewGuid():N}";

        var first = await CreateUserAsync(sub);
        var second = await CreateUserAsync(sub);

        Assert.Equal(first, second);
    }

    [PostgresFact]
    public async Task Should_ProvisionExactlyOneUser_When_FirstLoginsRace()
    {
        var sub = $"sub-{Guid.NewGuid():N}";

        var ids = await Task.WhenAll(Enumerable.Range(0, ParallelLogins).Select(_ => Task.Run(() => CreateUserAsync(sub))));

        await using var context = fixture.CreateContext();
        var stored = await context.Users.AsNoTracking().Where(u => u.CognitoSub == sub).Select(u => u.Id).ToListAsync();
        var winner = Assert.Single(stored);
        Assert.All(ids, id => Assert.Equal(winner, id));
    }

    [PostgresFact]
    public async Task Should_ThrowUniqueNameViolation_When_CategoryNameDiffersOnlyByCase()
    {
        var user = await fixture.UserAsync();
        var other = await fixture.UserAsync();
        await CreateCategoryAsync(user.Id, "Groceries");

        var violation = await Assert.ThrowsAsync<UniqueNameViolationException>(() => CreateCategoryAsync(user.Id, "GROCERIES"));

        Assert.Equal("UQ_Categories_User_Name", violation.ConstraintName);
        Assert.True(await CreateCategoryAsync(other.Id, "groceries") > 0);
    }
}
