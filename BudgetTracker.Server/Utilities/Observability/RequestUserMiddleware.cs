using BudgetTracker.Domain.Interfaces.Utilities;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>
/// Adds the internal UserId to the request's <see cref="RequestLogScope"/> once authentication has run. Uses
/// <see cref="ICurrentUserProvider"/> (scoped, so endpoints reuse the resolved user) and never the Cognito sub.
/// A user that cannot be resolved is left out of the scope; the request carries on unchanged.
/// </summary>
public sealed class RequestUserMiddleware(RequestDelegate next, ILogger<RequestUserMiddleware> logger)
{
    /// <summary>Runs the middleware for one request.</summary>
    /// <param name="context">The current request.</param>
    /// <returns>A task that completes when the rest of the pipeline has run.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && RequestLogScope.For(context) is { } scope
            && context.RequestServices.GetService<ICurrentUserProvider>() is { } currentUser)
        {
            try
            {
                scope.UserId = currentUser.UserId;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Only the type: provisioning messages can echo claim values. The endpoint surfaces the real failure.
                logger.LogWarning("Could not resolve the UserId for the log scope ({ExceptionType})", ex.GetType().Name);
            }
        }

        await next(context);
    }
}
