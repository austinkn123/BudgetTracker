using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Migrations;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace BudgetTracker.Tests.Data;

/// <summary>
/// Budget plan names must be unique per user and month regardless of case. Postgres compares text case-sensitively and
/// EF Core cannot model an expression index, so the unique index on (UserId, PlanMonth, lower("Name")) is raw SQL in the
/// migration. These tests stop a regeneration from silently reverting it.
/// </summary>
public class BudgetPlanNameUniquenessTests
{
    [Fact]
    public void Should_CreateUniqueIndexOnUserIdMonthAndLowerName_When_InitialCreateMigrationRuns()
    {
        var sql = new InitialCreate().UpOperations.OfType<SqlOperation>().Select(o => o.Sql).ToList();

        Assert.Contains(sql, s =>
            s.Contains("CREATE UNIQUE INDEX \"UQ_BudgetPlans_User_Month_Name\"") &&
            s.Contains("ON \"BudgetPlans\" (\"UserId\", \"PlanMonth\", lower(\"Name\"))"));
    }

    [Fact]
    public void Should_NotCreateCaseSensitiveIndex_When_InitialCreateMigrationRuns()
    {
        var indexes = new InitialCreate().UpOperations.OfType<CreateIndexOperation>();

        Assert.DoesNotContain(indexes, i => i.Name == "UQ_BudgetPlans_User_Month_Name");
    }

    [Fact]
    public void Should_DropLowerNameIndex_When_InitialCreateMigrationIsReverted()
    {
        var sql = new InitialCreate().DownOperations.OfType<SqlOperation>().Select(o => o.Sql).ToList();

        Assert.Contains(sql, s => s.Contains("DROP INDEX IF EXISTS \"UQ_BudgetPlans_User_Month_Name\""));
    }

    [Fact]
    public void Should_NotDeclareCaseSensitiveUniqueIndexOnUserIdMonthAndName_When_ModelIsBuilt()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new BudgetTrackerDbContext(options);

        var planIndexes = context.Model.FindEntityType(typeof(BudgetPlan))!.GetIndexes();

        Assert.DoesNotContain(planIndexes, i =>
            i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(BudgetPlan.UserId), nameof(BudgetPlan.PlanMonth), nameof(BudgetPlan.Name)]));
    }
}
