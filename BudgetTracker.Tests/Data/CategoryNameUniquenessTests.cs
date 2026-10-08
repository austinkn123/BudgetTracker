using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Migrations;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace BudgetTracker.Tests.Data;

/// <summary>
/// Category names must be unique per user regardless of case. Postgres compares text case-sensitively, and EF Core
/// cannot model an expression index, so the unique index on (UserId, lower("Name")) is raw SQL in the migration.
/// These tests stop a future migration regeneration from silently reverting to a case-sensitive index.
/// </summary>
public class CategoryNameUniquenessTests
{
    [Fact]
    public void Should_CreateUniqueIndexOnUserIdAndLowerName_When_InitialCreateMigrationRuns()
    {
        var sql = new InitialCreate().UpOperations.OfType<SqlOperation>().Select(o => o.Sql).ToList();

        Assert.Contains(sql, s =>
            s.Contains("CREATE UNIQUE INDEX \"UQ_Categories_User_Name\"") &&
            s.Contains("ON \"Categories\" (\"UserId\", lower(\"Name\"))"));
    }

    [Fact]
    public void Should_DropLowerNameIndex_When_InitialCreateMigrationIsReverted()
    {
        var sql = new InitialCreate().DownOperations.OfType<SqlOperation>().Select(o => o.Sql).ToList();

        Assert.Contains(sql, s => s.Contains("DROP INDEX IF EXISTS \"UQ_Categories_User_Name\""));
    }

    [Fact]
    public void Should_NotDeclareCaseSensitiveUniqueIndexOnUserIdAndName_When_ModelIsBuilt()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new BudgetTrackerDbContext(options);

        var categoryIndexes = context.Model.FindEntityType(typeof(Category))!.GetIndexes();

        Assert.DoesNotContain(categoryIndexes, i =>
            i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual([nameof(Category.UserId), nameof(Category.Name)]));
    }
}
