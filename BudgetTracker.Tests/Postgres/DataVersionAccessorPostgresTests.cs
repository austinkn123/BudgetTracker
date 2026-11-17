using BudgetTracker.Domain.Accessors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// <see cref="DataVersionAccessor"/> against real PostgreSQL, where concurrent bumps really contend for the row lock —
/// the SQLite in-memory tests share one connection and cannot show a lost update.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Postgres")]
public sealed class DataVersionAccessorPostgresTests(PostgresFixture fixture)
{
    private const int ParallelBumps = 20;

    private async Task<bool> BumpAsync(int userId)
    {
        await using var context = fixture.CreateContext();
        return await new DataVersionAccessor(context, NullLogger<DataVersionAccessor>.Instance).BumpAsync(userId);
    }

    private async Task<long> StoredVersionAsync(int userId)
    {
        await using var context = fixture.CreateContext();
        return await context.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DataVersion).SingleAsync();
    }

    [PostgresFact]
    public async Task Should_CountEveryBump_When_BumpedInParallelFromSeparateContexts()
    {
        var user = await fixture.UserAsync();
        var bystander = await fixture.UserAsync(dataVersion: 7);

        var results = await Task.WhenAll(Enumerable.Range(0, ParallelBumps).Select(_ => Task.Run(() => BumpAsync(user.Id))));

        Assert.All(results, Assert.True);
        Assert.Equal(ParallelBumps, await StoredVersionAsync(user.Id));
        Assert.Equal(7, await StoredVersionAsync(bystander.Id));
    }

    [PostgresFact]
    public async Task Should_ReturnFalse_When_BumpingUnknownUser()
    {
        Assert.False(await BumpAsync(int.MaxValue));
    }

    [PostgresFact]
    public async Task Should_ReturnVersionOrNull_When_ReadingByCognitoSub()
    {
        var user = await fixture.UserAsync(dataVersion: 41);
        await BumpAsync(user.Id);

        await using var context = fixture.CreateContext();
        var sut = new DataVersionAccessor(context, NullLogger<DataVersionAccessor>.Instance);

        Assert.Equal(42L, await sut.GetByCognitoSubAsync(user.CognitoSub!));
        Assert.Null(await sut.GetByCognitoSubAsync($"missing-{Guid.NewGuid():N}"));
    }

    [PostgresFact]
    public async Task Should_KeepTheBump_When_AUserLoadedBeforeItIsSavedAfterwards()
    {
        var user = await fixture.UserAsync();
        var renamedSub = $"renamed-{Guid.NewGuid():N}";

        await using var staleContext = fixture.CreateContext();
        var tracked = await staleContext.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(0, tracked.DataVersion);

        Assert.True(await BumpAsync(user.Id));
        tracked.CognitoSub = renamedSub;
        await staleContext.SaveChangesAsync();

        await using var context = fixture.CreateContext();
        var stored = await context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(renamedSub, stored.CognitoSub);
        Assert.Equal(1, stored.DataVersion);
    }
}
