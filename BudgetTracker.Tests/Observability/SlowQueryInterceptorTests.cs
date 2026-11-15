using BudgetTracker.Domain.Data;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Tests.Observability;

/// <summary>
/// The interceptor through a real relational provider (EF Core SQLite, in memory), so the durations and command text
/// are EF's own. A zero threshold makes every command "slow"; real slowness is not reproducible in a unit test.
/// </summary>
public sealed class SlowQueryInterceptorTests : IDisposable
{
    private const string SecretSub = "SECRET-COGNITO-SUB";
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeLogger<SlowQueryInterceptor> _logger = new();

    public SlowQueryInterceptorTests()
    {
        _connection.Open();
        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE "Users" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "CognitoSub" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "DataVersion" INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private BudgetTrackerDbContext BuildContext(int thresholdMs) =>
        new(new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(BuildSut(thresholdMs))
            .Options);

    private SlowQueryInterceptor BuildSut(int thresholdMs) =>
        new(Options.Create(new SlowQueryOptions { ThresholdMs = thresholdMs }), _logger);

    private static string Render(FakeLogRecord record) =>
        string.Join('\n', new[] { record.Message }.Concat(record.StructuredState!.Select(kv => $"{kv.Key}={kv.Value}")));

    [Fact]
    public async Task Should_WarnWithDurationStatementAndTables_When_AQueryReachesTheThreshold()
    {
        await using var context = BuildContext(thresholdMs: 0);

        await context.Users.Where(u => u.CognitoSub == SecretSub).Select(u => u.Id).ToListAsync();

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        var state = record.StructuredState!.ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Equal("SELECT", state["Statement"]);
        Assert.Equal("Users", state["Tables"]);
        Assert.Equal(nameof(CommandSource.LinqQuery), state["Source"]);
        Assert.True(double.Parse(state["DurationMs"]!) >= 0);
    }

    [Fact]
    public async Task Should_NeverLogSqlTextOrParameterValues()
    {
        await using var context = BuildContext(thresholdMs: 0);

        await context.Users.Where(u => u.CognitoSub == SecretSub).Select(u => u.Id).ToListAsync();
        // A literal, which EF inlines into the SQL text instead of sending as a parameter.
        await context.Users.Where(u => u.CognitoSub == "INLINED-LITERAL").Select(u => u.Id).ToListAsync();

        var rendered = string.Join('\n', _logger.Collector.GetSnapshot().Select(Render));
        Assert.DoesNotContain(SecretSub, rendered);
        Assert.DoesNotContain("INLINED-LITERAL", rendered);
        Assert.DoesNotContain("FROM", rendered);
        Assert.DoesNotContain("CognitoSub", rendered);
    }

    [Fact]
    public async Task Should_LogNothing_When_QueriesFinishUnderTheThreshold()
    {
        await using var context = BuildContext(thresholdMs: 60_000);

        await context.Users.Select(u => u.Id).ToListAsync();

        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    [Fact]
    public void Should_WarnForSynchronousQueriesToo()
    {
        using var context = BuildContext(thresholdMs: 0);

        _ = context.Users.Select(u => u.Id).ToList();

        Assert.Single(_logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task Should_WarnForWrites_When_TheyReachTheThreshold()
    {
        await using var context = BuildContext(thresholdMs: 0);

        await context.Users.Where(u => u.Id == 1).ExecuteUpdateAsync(s => s.SetProperty(u => u.DataVersion, 2));

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        var state = record.StructuredState!.ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Equal("UPDATE", state["Statement"]);
        Assert.Equal("Users", state["Tables"]);
    }

    [Theory]
    [InlineData(499, false)]
    [InlineData(500, true)]
    [InlineData(501, true)]
    public void Should_TreatTheThresholdAsInclusive(int durationMs, bool expectWarning)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT 1";

        BuildSut(thresholdMs: 500).Report(command, TimeSpan.FromMilliseconds(durationMs), CommandSource.LinqQuery);

        Assert.Equal(expectWarning ? 1 : 0, _logger.Collector.Count);
    }

    [Theory]
    [InlineData("""SELECT t."Id" FROM "Transactions" AS t INNER JOIN "Categories" AS c ON t."CategoryId" = c."Id" LEFT JOIN "Transactions" AS t0 ON TRUE""", "SELECT", "Transactions,Categories")]
    [InlineData("""INSERT INTO "PlaidItems" ("UserId") VALUES (@p0)""", "INSERT", "PlaidItems")]
    [InlineData("""DELETE FROM "Categories" WHERE "Id" = @p0""", "DELETE", "Categories")]
    [InlineData("""  update "Users" AS u SET "DataVersion" = 1""", "UPDATE", "Users")]
    [InlineData("SELECT 1", "SELECT", "")]
    [InlineData("", "", "")]
    public void Should_DescribeACommandByStatementAndDistinctTables(string sql, string statement, string tables)
    {
        var description = SlowQueryInterceptor.Describe(sql);

        Assert.Equal(statement, description.Statement);
        Assert.Equal(tables, description.Tables);
    }

    [Fact]
    public void Should_DefaultTheThresholdTo500Ms() => Assert.Equal(500, new SlowQueryOptions().ThresholdMs);
}
