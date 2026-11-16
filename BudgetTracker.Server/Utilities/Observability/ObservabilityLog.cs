using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>Source-generated log messages for the observability Utility. Event IDs 1000–1999.</summary>
public static partial class ObservabilityLog
{
    /// <summary>A bearer token failed validation. Only the exception type: messages can quote token contents.</summary>
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Authentication failed: {FailureType}")]
    public static partial void AuthenticationFailed(this ILogger logger, string failureType);

    /// <summary>A request ended in 401 or 403.</summary>
    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Request denied with {StatusCode} for {Method} {Path}")]
    public static partial void RequestDenied(this ILogger logger, int statusCode, string method, string? path);

    /// <summary>The signed-in user could not be resolved for the log scope; the request carries on.</summary>
    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Could not resolve the UserId for the log scope ({ExceptionType})")]
    public static partial void UserLookupFailed(this ILogger logger, string exceptionType);

    /// <summary>An exception reached the exception handler. Type and stack trace only, never a message.</summary>
    [LoggerMessage(EventId = 1004, Level = LogLevel.Error, Message = "Unhandled {ExceptionType} for {Method} {Path}\n{StackTrace}")]
    public static partial void UnhandledException(this ILogger logger, string? exceptionType, string method, string? path, string stackTrace);

    /// <summary>The readiness check threw. Only the exception type: driver messages can quote the host or user.</summary>
    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "Readiness check failed: {FailureType}")]
    public static partial void ReadinessCheckFailed(this ILogger logger, string failureType);

    /// <summary>A database command reached the slow-query threshold. Never the SQL text or parameter values.</summary>
    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning, Message = "Slow database command: {Statement} on {Tables} took {DurationMs} ms ({Source})")]
    public static partial void SlowDatabaseCommand(this ILogger logger, string statement, string tables, long durationMs, CommandSource source);
}
