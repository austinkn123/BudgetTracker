using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// The readiness probe and the slow-query interceptor through Npgsql, where the connection failures and the SQL text
/// are PostgreSQL's own rather than SQLite's.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Postgres")]
public sealed class ObservabilityPostgresTests(PostgresFixture fixture)
{
    private const string SecretSub = "SECRET-COGNITO-SUB";

    [PostgresFact]
    public async Task Should_ReportReady_When_ThePostgresDatabaseAnswers()
    {
        await using var context = fixture.CreateContext();

        Assert.True(await new DatabaseHealthAccessor(context).CanConnectAsync());
    }

    [PostgresFact]
    public async Task Should_ReportNotReadyRatherThanThrow_When_TheDatabaseDoesNotExist()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = $"bt_missing_{Guid.NewGuid():N}",
            Pooling = false,
        }.ConnectionString;
        await using var context = new BudgetTrackerDbContext(
            new DbContextOptionsBuilder<BudgetTrackerDbContext>().UseNpgsql(connectionString).Options);

        Assert.False(await new DatabaseHealthAccessor(context).CanConnectAsync());
    }

    [PostgresFact]
    public async Task Should_DescribeAnNpgsqlQueryByStatementAndTables_WithoutItsSqlOrParameters()
    {
        var logger = new FakeLogger<SlowQueryInterceptor>();
        var interceptor = new SlowQueryInterceptor(Options.Create(new SlowQueryOptions { ThresholdMs = 0 }), logger);
        await using var context = new BudgetTrackerDbContext(
            new DbContextOptionsBuilder<BudgetTrackerDbContext>()
                .UseNpgsql(fixture.ConnectionString)
                .AddInterceptors(interceptor)
                .Options);

        // A join, so Npgsql's own quoting and aliasing of two tables is what gets parsed.
        await context.Transactions
            .Where(t => t.Payee == SecretSub && t.Category!.Name == "Groceries")
            .Select(t => t.Id)
            .ToListAsync();

        var record = Assert.Single(logger.Collector.GetSnapshot());
        var state = record.StructuredState!.ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Equal("SELECT", state["Statement"]);
        Assert.Equal("Transactions,Categories", state["Tables"]);
        var rendered = string.Join('\n', state.Select(kv => $"{kv.Key}={kv.Value}")) + record.Message;
        Assert.DoesNotContain(SecretSub, rendered);
        Assert.DoesNotContain("Groceries", rendered);
        Assert.DoesNotContain("Payee", rendered);
        Assert.Equal(LogLevel.Warning, record.Level);
    }
}
