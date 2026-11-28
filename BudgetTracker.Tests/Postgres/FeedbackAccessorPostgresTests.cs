using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Tests.Postgres;

/// <summary>FeedbackAccessor against real Postgres: the migration's constraints, UTC timestamps and the rate-limit count.</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Postgres")]
public sealed class FeedbackAccessorPostgresTests(PostgresFixture fixture)
{
    [PostgresFact]
    public async Task Should_SaveAndCountAnEntry_When_CreatedAtIsUtc()
    {
        var user = await fixture.UserAsync();
        var now = DateTime.UtcNow;
        await using (var context = fixture.CreateContext())
            await new FeedbackAccessor(context).CreateAsync(new Feedback { UserId = user.Id, Message = "nice", Page = "/", CreatedAt = now });

        await using var read = fixture.CreateContext();
        Assert.Equal(1, await new FeedbackAccessor(read).CountSinceAsync(user.Id, now.AddHours(-1)));
        Assert.Equal(0, await new FeedbackAccessor(read).CountSinceAsync(user.Id, now.AddMinutes(1)));
    }

    [PostgresFact]
    public async Task Should_RejectABlankMessage_When_TheEngineIsBypassed()
    {
        var user = await fixture.UserAsync();
        await using var context = fixture.CreateContext();

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            new FeedbackAccessor(context).CreateAsync(new Feedback { UserId = user.Id, Message = "   ", CreatedAt = DateTime.UtcNow }));

        Assert.Contains("CK_Feedback_Message", ex.InnerException?.Message);
    }

    [PostgresFact]
    public async Task Should_RejectAMessageOverTheColumnLimit()
    {
        var user = await fixture.UserAsync();
        await using var context = fixture.CreateContext();

        await Assert.ThrowsAsync<DbUpdateException>(() => new FeedbackAccessor(context).CreateAsync(
            new Feedback { UserId = user.Id, Message = new string('a', FeedbackLimits.MaxMessageLength + 1), CreatedAt = DateTime.UtcNow }));
    }
}
