using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>How one Webull order-history sync call ended, as recorded on the sync metric event.</summary>
public enum WebullOrderSyncOutcome
{
    /// <summary>Every current account was paged until nothing was left to fetch.</summary>
    Completed,

    /// <summary>The page cap or the time box ended the call early; the next call resumes from the saved progress.</summary>
    CutShort,

    /// <summary>Webull failed; the failed progress is saved and the failure is logged as its own warning.</summary>
    Failed,
}

/// <summary>How one Webull portfolio fetch ended, as recorded on the fetch metric event.</summary>
public enum WebullPortfolioFetchOutcome
{
    /// <summary>Balances and positions were fetched and saved.</summary>
    Completed,

    /// <summary>The fetch or the save failed; the failure is logged as its own warning.</summary>
    Failed,
}

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

    /// <summary>
    /// The order-history sync metric: one event per sync call that reached Webull. Counts and duration only, never
    /// order data, so Logs Insights can aggregate it freely.
    /// </summary>
    [LoggerMessage(EventId = 3005, Level = LogLevel.Information, Message = "Webull order history sync for user {UserId} finished: {Outcome}; {Pages} pages, {Orders} orders in {DurationMs} ms")]
    public static partial void OrderSyncFinished(this ILogger logger, int userId, WebullOrderSyncOutcome outcome, int pages, int orders, long durationMs);

    /// <summary>
    /// The portfolio-fetch metric: one event per balances-and-positions fetch, from a refresh or the close snapshot.
    /// Outcome and duration only, never balances or holdings.
    /// </summary>
    [LoggerMessage(EventId = 3006, Level = LogLevel.Information, Message = "Webull portfolio fetch for user {UserId} finished: {Outcome} in {DurationMs} ms")]
    public static partial void PortfolioFetchFinished(this ILogger logger, int userId, WebullPortfolioFetchOutcome outcome, long durationMs);

    /// <summary>One tick of the close-snapshot service failed; the next interval retries.</summary>
    [LoggerMessage(EventId = 3004, Level = LogLevel.Error, Message = "Webull close snapshot tick failed; will retry on the next interval.")]
    public static partial void CloseSnapshotTickFailed(this ILogger logger, Exception exception);
}
