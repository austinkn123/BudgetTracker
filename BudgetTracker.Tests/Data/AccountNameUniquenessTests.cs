using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Migrations;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace BudgetTracker.Tests.Data;

/// <summary>
/// Account names must be unique per user regardless of case. SQL Server's case-insensitive collation used to give this
/// for free; Postgres compares text case-sensitively and EF Core cannot model an expression index, so the unique index
/// on (UserId, lower("Name")) is raw SQL in the migration. These tests stop a regeneration from silently reverting it.
/// </summary>
public class AccountNameUniquenessTests
{
    [Fact]
    public void Should_CreateUniqueIndexOnUserIdAndLowerName_When_InitialCreateMigrationRuns()
    {
        var sql = new InitialCreate().UpOperations.OfType<SqlOperation>().Select(o => o.Sql).ToList();

        Assert.Contains(sql, s =>
            s.Contains("CREATE UNIQUE INDEX \"UQ_Accounts_User_Name\"") &&
            s.Contains("ON \"Accounts\" (\"UserId\", lower(\"Name\"))"));
    }

    [Fact]
    public void Should_NotCreateCaseSensitiveIndex_When_InitialCreateMigrationRuns()
    {
        var indexes = new InitialCreate().UpOperations.OfType<CreateIndexOperation>();

        Assert.DoesNotContain(indexes, i => i.Name == "UQ_Accounts_User_Name");
    }

    [Fact]
    public void Should_DropLowerNameIndex_When_InitialCreateMigrationIsReverted()
    {
        var sql = new InitialCreate().DownOperations.OfType<SqlOperation>().Select(o => o.Sql).ToList();

        Assert.Contains(sql, s => s.Contains("DROP INDEX IF EXISTS \"UQ_Accounts_User_Name\""));
    }

    [Fact]
    public void Should_NotDeclareCaseSensitiveUniqueIndexOnUserIdAndName_When_ModelIsBuilt()
    {
        var accountIndexes = BuildModel().FindEntityType(typeof(Account))!.GetIndexes();

        Assert.DoesNotContain(accountIndexes, i =>
            i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual([nameof(Account.UserId), nameof(Account.Name)]));
    }

    [Fact]
    public void Should_KeepAnIndexLeadingWithUserId_When_ModelIsBuilt()
    {
        // The per-user list query and the Users FK both need an index that starts with UserId.
        var accountIndexes = BuildModel().FindEntityType(typeof(Account))!.GetIndexes();

        Assert.Contains(accountIndexes, i => i.Properties[0].Name == nameof(Account.UserId));
    }

    private static Microsoft.EntityFrameworkCore.Metadata.IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new BudgetTrackerDbContext(options);
        return context.Model;
    }
}
