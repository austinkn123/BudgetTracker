namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>
/// Logs every 401/403 response to <see cref="SecurityLog.Category"/>. The <see cref="RequestLogScope"/> supplies the
/// correlation ID and, when known, the UserId. Only the status, method and path are logged — never headers or bodies.
/// </summary>
public sealed class DeniedRequestLoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger(SecurityLog.Category);

    /// <summary>Runs the middleware for one request.</summary>
    /// <param name="context">The current request.</param>
    /// <returns>A task that completes when the rest of the pipeline has run.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.Response.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
        {
            _logger.LogWarning(
                "Request denied with {StatusCode} for {Method} {Path}",
                context.Response.StatusCode, context.Request.Method, context.Request.Path.Value);
        }
    }
}
