using System.Text;
using Microsoft.AspNetCore.Diagnostics;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>
/// Logs each unhandled exception once, with the exception type and stack trace of it and every inner exception, but
/// never a message: messages can carry payees, amounts or free-text errors (project-docs/observability.md §3).
/// Returns <c>false</c> so the exception handler middleware still writes the ProblemDetails response. The middleware's
/// own diagnostic log line, which includes the full exception, is switched off in
/// <see cref="ObservabilityExtensions.AddBudgetTrackerObservability"/>.
/// </summary>
public sealed class UnhandledExceptionLogger(ILogger<UnhandledExceptionLogger> logger) : IExceptionHandler
{
    private const int MaxDepth = 8;

    /// <summary>Logs the sanitized exception; runs inside the request's <see cref="RequestLogScope"/>.</summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="exception">The unhandled exception.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>false</c>, so the default ProblemDetails response is written.</returns>
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(
            "Unhandled {ExceptionType} for {Method} {Path}\n{StackTrace}",
            exception.GetType().FullName, httpContext.Request.Method, httpContext.Request.Path.Value, Describe(exception));
        return ValueTask.FromResult(false);
    }

    /// <summary>The type and stack trace of <paramref name="exception"/> and its inner exceptions, without messages.</summary>
    /// <param name="exception">The exception to describe.</param>
    /// <returns>A message-free description.</returns>
    public static string Describe(Exception exception)
    {
        var text = new StringBuilder();
        var depth = 0;
        for (var current = exception; current is not null && depth < MaxDepth; current = current.InnerException, depth++)
        {
            if (depth > 0)
                text.Append("--- Inner ").AppendLine(current.GetType().FullName);
            text.AppendLine(current.StackTrace);
        }
        return text.ToString();
    }
}
