using BudgetTracker.Domain.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// One throwaway PostgreSQL database per test run, built by the real EF migrations, for tests that must see Npgsql's
/// SQL translation, Postgres types and row locking rather than the InMemory/SQLite stand-ins.
/// </summary>
/// <remarks>
/// <para>Connects to <c>BUDGETTRACKER_TEST_POSTGRES</c> (default <c>Host=localhost;Username=postgres;Password=postgres</c>),
/// creates <c>bt_test_&lt;guid&gt;</c>, migrates it, and drops it on dispose. It never opens, alters or drops any other
/// database: the only statements sent to the server outside its own database are that CREATE and that DROP.</para>
/// <para>The database is shared by every test in <see cref="PostgresCollection"/>, which run one at a time; each test
/// still seeds its own user so no test depends on another's rows.</para>
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"bt_test_{Guid.NewGuid():N}";
    private string? _connectionString;
    private bool _created;

    /// <summary>The name of the database this fixture created.</summary>
    public string DatabaseName => _databaseName;

    /// <summary>A new context on the test database. The caller disposes it.</summary>
    /// <returns>A context with an empty change tracker and its own connection.</returns>
    public BudgetTrackerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseNpgsql(_connectionString ?? throw new InvalidOperationException("The PostgreSQL test database was not created."))
            .Options);

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (PostgresTestServer.UnreachableReason is { } reason)
        {
            // Not required: every [PostgresFact] is already skipped, so there is nothing to set up.
            if (!PostgresTestServer.IsRequired)
                return;

            throw new InvalidOperationException($"{PostgresTestServer.RequireVariable} is set but {reason}");
        }

        var admin = PostgresTestServer.AdminConnection();
        await ExecuteOnServerAsync(admin, $"CREATE DATABASE \"{_databaseName}\"");
        _created = true;

        _connectionString = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = _databaseName }.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        // Only a database this instance created is ever dropped.
        if (!_created)
            return;

        // FORCE terminates the pooled connections server-side; drop them client-side first so none is reused.
        if (_connectionString is not null)
        {
            await using var pooled = new NpgsqlConnection(_connectionString);
            NpgsqlConnection.ClearPool(pooled);
        }

        await ExecuteOnServerAsync(PostgresTestServer.AdminConnection(), $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    private static async Task ExecuteOnServerAsync(NpgsqlConnectionStringBuilder admin, string sql)
    {
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Pooling = false }.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>Shares one <see cref="PostgresFixture"/> database across the real-PostgreSQL test classes.</summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    /// <summary>The collection name to put in <c>[Collection]</c>.</summary>
    public const string Name = "Postgres";
}
