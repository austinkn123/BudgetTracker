using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Tests.Accessors;

public class DatabaseHealthAccessorTests
{
    [Fact]
    public async Task Should_ReturnTrue_When_TheDatabaseAnswers()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new BudgetTrackerDbContext(options);

        Assert.True(await new DatabaseHealthAccessor(context).CanConnectAsync());
    }

    [Fact]
    public async Task Should_ReturnFalseRatherThanThrow_When_TheDatabaseIsUnreachable()
    {
        // A relational provider pointed at a database that does not exist: the unreachable case, without a network.
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.db")};Mode=ReadOnly")
            .Options;
        await using var context = new BudgetTrackerDbContext(options);

        Assert.False(await new DatabaseHealthAccessor(context).CanConnectAsync());
    }
}
