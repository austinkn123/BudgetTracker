using Microsoft.Extensions.Logging;
using Moq;

namespace BudgetTracker.Tests;

/// <summary>Mock loggers for tests that verify a log call was made.</summary>
internal static class TestLoggers
{
    /// <summary>
    /// A mock logger with every level enabled, like a real one. Source-generated log methods ask
    /// <see cref="ILogger.IsEnabled"/> first, and a bare mock answers false, so nothing would ever be logged.
    /// </summary>
    public static Mock<ILogger<T>> Enabled<T>()
    {
        var logger = new Mock<ILogger<T>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return logger;
    }

    /// <summary>One captured log call: its level, event ID and structured fields as "name=value" lines.</summary>
    public sealed record Entry(LogLevel Level, int EventId, string Fields);

    /// <summary>
    /// Records every call made to <paramref name="logger"/> from now on. Source-generated log state is pooled and
    /// cleared once the call returns, so its fields have to be read while the call is in flight, not in a later Verify.
    /// </summary>
    public static IReadOnlyList<Entry> Capture<T>(Mock<ILogger<T>> logger)
    {
        var entries = new List<Entry>();
        logger.Setup(l => l.Log(
                It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation =>
            {
                var fields = invocation.Arguments[2] is IEnumerable<KeyValuePair<string, object?>> state
                    ? string.Join('\n', state.Select(kv => $"{kv.Key}={kv.Value}"))
                    : string.Empty;
                entries.Add(new Entry((LogLevel)invocation.Arguments[0], ((EventId)invocation.Arguments[1]).Id, fields));
            }));
        return entries;
    }
}
