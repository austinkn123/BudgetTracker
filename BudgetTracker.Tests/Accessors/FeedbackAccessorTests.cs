using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Tests.Accessors;

public class FeedbackAccessorTests : IDisposable
{
    private readonly BudgetTrackerDbContext _context = new(
        new DbContextOptionsBuilder<BudgetTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _context.Dispose();

    private FeedbackAccessor BuildSut() => new(_context);

    private async Task<int> SeedAsync(int userId, DateTime createdAt, string message = "hi") =>
        await BuildSut().CreateAsync(new Feedback { UserId = userId, Message = message, CreatedAt = createdAt });

    [Fact]
    public async Task Should_CountOnlyTheUsersEntriesAtOrAfterTheCutoff()
    {
        var cutoff = new DateTime(2026, 11, 28, 0, 0, 0);
        await SeedAsync(5, cutoff.AddMinutes(-1));
        await SeedAsync(5, cutoff);
        await SeedAsync(5, cutoff.AddMinutes(30));
        await SeedAsync(6, cutoff.AddMinutes(30));

        Assert.Equal(2, await BuildSut().CountSinceAsync(5, cutoff));
    }

    [Fact]
    public async Task Should_PageEveryUsersEntriesNewestFirstById()
    {
        var at = new DateTime(2026, 11, 28);
        var ids = new List<int>();
        for (var i = 0; i < 5; i++)
            ids.Add(await SeedAsync(i % 2 + 1, at, $"m{i}"));

        var first = await BuildSut().GetPageAsync(null, 2);
        var next = await BuildSut().GetPageAsync(first[^1].Id, 2);

        Assert.Equal([ids[4], ids[3]], first.Select(f => f.Id));
        Assert.Equal([ids[2], ids[1]], next.Select(f => f.Id));
    }
}
