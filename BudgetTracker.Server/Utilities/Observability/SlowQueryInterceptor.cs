using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>
/// Warns when a database command takes at least <see cref="SlowQueryOptions.ThresholdMs"/>. It logs the duration, the
/// statement type and the table names only: never the SQL text or parameter values, which can carry payees, amounts
/// and identifiers (project-docs/observability.md §3). EF's own command logging stays capped at Warning in production
/// for the same reason, so this is the only slow-query signal there.
/// </summary>
/// <remarks>For queries, the duration is the time until the first result is available, not the time to read every row.</remarks>
public sealed partial class SlowQueryInterceptor(IOptions<SlowQueryOptions> options, ILogger<SlowQueryInterceptor> logger)
    : DbCommandInterceptor
{
    private readonly TimeSpan _threshold = TimeSpan.FromMilliseconds(options.Value.ThresholdMs);

    /// <summary>The statement type and tables of a command, safe to log.</summary>
    /// <param name="Statement">The leading SQL keyword, upper-cased (SELECT, INSERT, UPDATE, DELETE); empty when there is none.</param>
    /// <param name="Tables">The distinct table names the command touches, comma-separated in first-seen order.</param>
    public readonly record struct CommandDescription(string Statement, string Tables);

    /// <summary>Logs a warning when <paramref name="duration"/> reaches the threshold.</summary>
    /// <param name="command">The executed command; only its statement type and table names are read.</param>
    /// <param name="duration">How long the command took.</param>
    /// <param name="source">What issued the command (a LINQ query, SaveChanges, ...).</param>
    public void Report(DbCommand command, TimeSpan duration, CommandSource source)
    {
        if (duration < _threshold)
            return;

        var (statement, tables) = Describe(command.CommandText);
        logger.LogWarning(
            "Slow database command: {Statement} on {Tables} took {DurationMs} ms ({Source})",
            statement, tables, (long)Math.Round(duration.TotalMilliseconds), source);
    }

    /// <summary>Reduces SQL text to its statement type and table names.</summary>
    /// <param name="sql">The command text.</param>
    /// <returns>A description with no literals, parameters or column names.</returns>
    public static CommandDescription Describe(string sql)
    {
        var statement = LeadingKeyword().Match(sql).Value.ToUpperInvariant();
        var tables = TableReference().Matches(sql).Select(m => m.Groups[1].Value).Distinct();
        return new CommandDescription(statement, string.Join(',', tables));
    }

    [GeneratedRegex(@"[A-Za-z]+")]
    private static partial Regex LeadingKeyword();

    // A quoted identifier after FROM / JOIN / UPDATE / INTO, with an optional schema prefix.
    [GeneratedRegex("""\b(?:FROM|JOIN|UPDATE|INTO)\s+(?:"[^"]+"\.)?"([^"]+)"\s*""", RegexOptions.IgnoreCase)]
    private static partial Regex TableReference();

    /// <inheritdoc />
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Report(command, eventData.Duration, eventData.CommandSource);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        Report(command, eventData.Duration, eventData.CommandSource);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Report(command, eventData.Duration, eventData.CommandSource);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Report(command, eventData.Duration, eventData.CommandSource);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Report(command, eventData.Duration, eventData.CommandSource);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        Report(command, eventData.Duration, eventData.CommandSource);
        return ValueTask.FromResult(result);
    }
}
