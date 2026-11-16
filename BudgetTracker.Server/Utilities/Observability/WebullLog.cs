using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>Source-generated log messages for the Webull workflows. Event IDs 3000–3999.</summary>
public static partial class WebullLog
{
    /// <summary>A Webull call failed.</summary>
    [LoggerMessage(EventId = 3001, Level = LogLevel.Warning, Message = "Webull {Operation} failed ({Kind}).")]
    public static partial void OperationFailed(this ILogger logger, Exception exception, string operation, WebullErrorKind kind);

    /// <summary>The market-close snapshot failed for one user; the other users still run.</summary>
    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning, Message = "Webull close snapshot failed for user {UserId}.")]
    public static partial void CloseSnapshotFailed(this ILogger logger, Exception exception, int userId);

    /// <summary>Webull throttled a call; it is retried after a back-off.</summary>
    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning, Message = "Webull {Operation} hit HTTP 429; retry {Attempt} of {MaxRetries} in {Seconds}s.")]
    public static partial void Throttled(this ILogger logger, string operation, int attempt, int maxRetries, double seconds);

    /// <summary>One tick of the close-snapshot service failed; the next interval retries.</summary>
    [LoggerMessage(EventId = 3004, Level = LogLevel.Error, Message = "Webull close snapshot tick failed; will retry on the next interval.")]
    public static partial void CloseSnapshotTickFailed(this ILogger logger, Exception exception);
}
