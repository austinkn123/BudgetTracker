using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace BudgetTracker.Tests.Data;

/// <summary>
/// Timestamp columns are <c>timestamp without time zone</c> and the app writes <see cref="DateTime.UtcNow"/>. A bare
/// <c>now()</c> default is converted using the session time zone, so database-generated values must be pinned to UTC.
/// </summary>
public class UtcTimestampDefaultTests
{
    private const string UtcNow = "(now() AT TIME ZONE 'utc')";

    [Fact]
    public void Should_UseUtcNowForEverySqlDefault_When_ModelIsBuilt()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new BudgetTrackerDbContext(options);

        var defaults = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Select(p => p.GetDefaultValueSql())
            .Where(sql => sql is not null)
            .ToList();

        Assert.NotEmpty(defaults);
        Assert.All(defaults, sql => Assert.Equal(UtcNow, sql));
    }

    [Fact]
    public void Should_UseUtcNowForEverySqlDefault_When_InitialCreateMigrationRuns()
    {
        var defaults = new InitialCreate().UpOperations.OfType<CreateTableOperation>()
            .SelectMany(t => t.Columns)
            .Select(c => c.DefaultValueSql)
            .Where(sql => sql is not null)
            .ToList();

        Assert.NotEmpty(defaults);
        Assert.All(defaults, sql => Assert.Equal(UtcNow, sql));
    }
}
