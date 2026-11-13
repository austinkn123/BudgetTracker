using Amazon.Lambda.AspNetCoreServer;
using Amazon.Lambda.Core;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>
/// Assigns the request's correlation ID, echoes it on the response, and opens the <see cref="RequestLogScope"/> that
/// every log line of the request is written inside.
/// </summary>
public sealed class RequestCorrelationMiddleware(RequestDelegate next, ILogger<RequestCorrelationMiddleware> logger)
{
    /// <summary>Runs the middleware for one request.</summary>
    /// <param name="context">The current request.</param>
    /// <returns>A task that completes when the rest of the pipeline has run.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var inbound = context.Request.Headers[CorrelationId.HeaderName].ToString();
        var correlationId = CorrelationId.IsValid(inbound) ? inbound : CorrelationId.New();
        CorrelationId.Set(context, correlationId);

        // OnStarting rather than setting the header now: the exception handler clears response headers before writing.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationId.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Amazon.Lambda.AspNetCoreServer puts the invocation's ILambdaContext in Items; it is absent when running locally.
        var lambdaContext = context.Items[AbstractAspNetCoreFunction.LAMBDA_CONTEXT] as ILambdaContext;
        var scope = new RequestLogScope(correlationId, lambdaContext?.AwsRequestId);
        scope.AttachTo(context);

        using (logger.BeginScope(scope))
        {
            await next(context);
        }
    }
}
