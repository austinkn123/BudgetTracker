using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// The committed migrations, applied to an empty PostgreSQL database by <see cref="PostgresFixture"/>: they all run, they
/// leave the schema the model expects, and the raw-SQL expression indexes EF cannot model are really there.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Postgres")]
public sealed class MigrationsPostgresTests(PostgresFixture fixture)
{
    private async Task<string?> IndexDefinitionAsync(string indexName)
    {
        await using var context = fixture.CreateContext();
        return await context.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public' AND indexname = {indexName}")
            .SingleOrDefaultAsync();
    }

    [PostgresFact]
    public async Task Should_ApplyEveryMigration_When_MigratingAnEmptyDatabase()
    {
        await using var context = fixture.CreateContext();

        var defined = context.Database.GetMigrations().ToList();
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();

        Assert.NotEmpty(defined);
        Assert.Equal(defined, applied);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [PostgresFact]
    public async Task Should_HaveNoPendingModelChanges_When_ModelIsComparedToTheSnapshot()
    {
        await using var context = fixture.CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Should_CreatePayeeLookupIndex_When_MigrationsAreApplied()
    {
        var definition = await IndexDefinitionAsync("IX_Transactions_AccountId_LowerPayee_OccurredAt");

        Assert.NotNull(definition);
        Assert.Contains("ON public.\"Transactions\"", definition);
        Assert.Contains("(\"AccountId\", lower((\"Payee\")::text), \"OccurredAt\" DESC, \"Id\" DESC)", definition);
        Assert.Contains("WHERE ((\"CategoryId\" IS NOT NULL) AND (\"Payee\" IS NOT NULL))", definition);
    }

    [PostgresFact]
    public async Task Should_CreateCaseInsensitiveUniqueNameIndexes_When_MigrationsAreApplied()
    {
        var categories = await IndexDefinitionAsync("UQ_Categories_User_Name");
        var accounts = await IndexDefinitionAsync("UQ_Accounts_User_Name");
        var plans = await IndexDefinitionAsync("UQ_BudgetPlans_User_Month_Name");

        Assert.StartsWith("CREATE UNIQUE INDEX", categories);
        Assert.Contains("(\"UserId\", lower((\"Name\")::text))", categories);
        Assert.StartsWith("CREATE UNIQUE INDEX", accounts);
        Assert.Contains("(\"UserId\", lower((\"Name\")::text))", accounts);
        Assert.StartsWith("CREATE UNIQUE INDEX", plans);
        Assert.Contains("(\"UserId\", \"PlanMonth\", lower((\"Name\")::text))", plans);
    }
}
