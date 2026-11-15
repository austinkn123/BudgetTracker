using Npgsql;

namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// Where the real-PostgreSQL tests connect, and whether that server answers. Probed once per test run.
/// </summary>
internal static class PostgresTestServer
{
    /// <summary>Environment variable holding an admin connection string (a role allowed to CREATE DATABASE).</summary>
    public const string ConnectionVariable = "BUDGETTRACKER_TEST_POSTGRES";

    /// <summary>
    /// Environment variable that, when set to <c>1</c> or <c>true</c> (any casing), turns "server unreachable" from a
    /// skip into a failure.
    /// </summary>
    public const string RequireVariable = "BUDGETTRACKER_REQUIRE_POSTGRES";

    private const string DefaultConnectionString = "Host=localhost;Username=postgres;Password=postgres";
    private const string MaintenanceDatabase = "postgres";
    private const int ProbeTimeoutSeconds = 3;

    private static readonly Lazy<string?> Probe = new(() => ProbeServer(Environment.GetEnvironmentVariable(ConnectionVariable)));

    /// <summary>
    /// The configured server, pointed at its maintenance database. Only ever used to issue CREATE/DROP DATABASE for the
    /// fixture's own uniquely named database.
    /// </summary>
    public static NpgsqlConnectionStringBuilder AdminConnection() =>
        AdminConnection(Environment.GetEnvironmentVariable(ConnectionVariable));

    private static NpgsqlConnectionStringBuilder AdminConnection(string? configured)
    {
        var builder = new NpgsqlConnectionStringBuilder(string.IsNullOrWhiteSpace(configured) ? DefaultConnectionString : configured);
        if (string.IsNullOrWhiteSpace(builder.Database))
            builder.Database = MaintenanceDatabase;
        return builder;
    }

    /// <summary>True when an unreachable server must fail the tests instead of skipping them (CI).</summary>
    public static bool IsRequired => IsRequiredValue(Environment.GetEnvironmentVariable(RequireVariable));

    /// <summary>Null when the server accepted a login; otherwise why not, worded for a skip/failure message.</summary>
    public static string? UnreachableReason => Probe.Value;

    /// <summary>Whether a <see cref="RequireVariable"/> value asks for the server to be mandatory.</summary>
    /// <param name="value">The raw environment value, or null when unset.</param>
    /// <returns>True for <c>1</c> or <c>true</c> in any casing, ignoring surrounding whitespace.</returns>
    internal static bool IsRequiredValue(string? value) =>
        value?.Trim() is { } flag && (flag == "1" || flag.Equals("true", StringComparison.OrdinalIgnoreCase));

    /// <summary>Tries one login with the given admin connection string (null/blank means the default).</summary>
    /// <param name="configured">The raw <see cref="ConnectionVariable"/> value.</param>
    /// <returns>Null when the login succeeded; otherwise the reason. Never throws.</returns>
    internal static string? ProbeServer(string? configured)
    {
        NpgsqlConnectionStringBuilder admin;
        try
        {
            admin = AdminConnection(configured);
        }
        catch (Exception ex)
        {
            return $"{ConnectionVariable} is not a valid connection string ({ex.Message.TrimEnd('.')}).";
        }

        try
        {
            // Unpooled so the probe leaves nothing behind; short timeout so a missing server costs seconds, once.
            using var connection = new NpgsqlConnection(
                new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Timeout = ProbeTimeoutSeconds, Pooling = false }.ConnectionString);
            connection.Open();
            return null;
        }
        catch (Exception ex)
        {
            // Everything is caught: this runs while xUnit constructs [PostgresFact] during discovery, where an escaping
            // exception drops the tests from the run instead of skipping or failing them (e.g. Npgsql throws
            // ArgumentException for a connection string with no Host).
            // Host and port only: the connection string carries a password.
            return $"PostgreSQL not reachable at {(string.IsNullOrWhiteSpace(admin.Host) ? "<no host>" : admin.Host)}:{admin.Port} " +
                   $"({ex.Message.TrimEnd('.')}); set {ConnectionVariable} to an admin connection string to run these tests.";
        }
    }
}
