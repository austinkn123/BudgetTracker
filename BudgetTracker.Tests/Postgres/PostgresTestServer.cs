using Npgsql;

namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// Where the real-PostgreSQL tests connect, and whether that server answers. Probed once per test run.
/// </summary>
internal static class PostgresTestServer
{
    /// <summary>Environment variable holding an admin connection string (a role allowed to CREATE DATABASE).</summary>
    public const string ConnectionVariable = "BUDGETTRACKER_TEST_POSTGRES";

    /// <summary>Environment variable that, when set to <c>1</c>, turns "server unreachable" from a skip into a failure.</summary>
    public const string RequireVariable = "BUDGETTRACKER_REQUIRE_POSTGRES";

    private const string DefaultConnectionString = "Host=localhost;Username=postgres;Password=postgres";
    private const string MaintenanceDatabase = "postgres";
    private const int ProbeTimeoutSeconds = 3;

    private static readonly Lazy<string?> Probe = new(ProbeServer);

    /// <summary>
    /// The configured server, pointed at its maintenance database. Only ever used to issue CREATE/DROP DATABASE for the
    /// fixture's own uniquely named database.
    /// </summary>
    public static NpgsqlConnectionStringBuilder AdminConnection()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionVariable);
        var builder = new NpgsqlConnectionStringBuilder(string.IsNullOrWhiteSpace(configured) ? DefaultConnectionString : configured);
        if (string.IsNullOrWhiteSpace(builder.Database))
            builder.Database = MaintenanceDatabase;
        return builder;
    }

    /// <summary>True when an unreachable server must fail the tests instead of skipping them (CI).</summary>
    public static bool IsRequired => Environment.GetEnvironmentVariable(RequireVariable) == "1";

    /// <summary>Null when the server accepted a login; otherwise why not, worded for a skip/failure message.</summary>
    public static string? UnreachableReason => Probe.Value;

    private static string? ProbeServer()
    {
        NpgsqlConnectionStringBuilder admin;
        try
        {
            admin = AdminConnection();
        }
        catch (ArgumentException ex)
        {
            return $"{ConnectionVariable} is not a valid connection string ({ex.Message}).";
        }

        try
        {
            // Unpooled so the probe leaves nothing behind; short timeout so a missing server costs seconds, once.
            using var connection = new NpgsqlConnection(
                new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Timeout = ProbeTimeoutSeconds, Pooling = false }.ConnectionString);
            connection.Open();
            return null;
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or TimeoutException or System.Net.Sockets.SocketException)
        {
            // Host and port only: the connection string carries a password.
            return $"PostgreSQL not reachable at {admin.Host}:{admin.Port} ({ex.Message.TrimEnd('.')}); " +
                   $"set {ConnectionVariable} to an admin connection string to run these tests.";
        }
    }
}
