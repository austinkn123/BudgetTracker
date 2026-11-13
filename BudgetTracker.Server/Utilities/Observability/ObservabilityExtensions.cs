using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging.Console;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>
/// The observability Utility (project-docs/observability.md §5): structured JSON logs, correlation IDs, ProblemDetails
/// errors, <c>/health</c>, and security logging. Endpoints and Engines do not log; this wiring covers them.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>The anonymous liveness endpoint. It runs no health checks, so it never touches the database.</summary>
    public const string HealthPath = "/health";

    private const string BuiltInExceptionHandlerCategory = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";

    /// <summary>
    /// Registers JSON console logging (with scopes), the production log-level caps, ProblemDetails, health checks and
    /// failed-authentication logging.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same builder.</returns>
    public static WebApplicationBuilder AddBudgetTrackerObservability(this WebApplicationBuilder builder)
    {
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });

        // The built-in handler logs the full exception, message included; UnhandledExceptionLogger logs a sanitized one.
        builder.Logging.AddFilter(BuiltInExceptionHandlerCategory, LogLevel.None);

        if (!builder.Environment.IsDevelopment())
        {
            // Keeps SQL text, parameters, outbound URLs and inbound URLs with query strings (ASP.NET Core hosting logs
            // them at Information) out of CloudWatch (§3). Code rules win over appsettings.
            builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
            builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        }

        builder.Services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["correlationId"] = CorrelationId.Get(context.HttpContext));

        builder.Services.AddExceptionHandler<UnhandledExceptionLogger>();
        builder.Services.AddHealthChecks();

        builder.Services.PostConfigureAll<JwtBearerOptions>(options =>
        {
            options.Events ??= new JwtBearerEvents();
            var inner = options.Events.OnAuthenticationFailed;
            options.Events.OnAuthenticationFailed = async context =>
            {
                // The exception type only: messages can quote token contents.
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(SecurityLog.Category)
                    .LogWarning("Authentication failed: {FailureType}", context.Exception.GetType().Name);
                await inner(context);
            };
        });

        return builder;
    }

    /// <summary>
    /// Adds the correlation, exception-handling and denied-request middleware, and maps <see cref="HealthPath"/>.
    /// Call first in the pipeline, before static files, authentication and endpoints.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static WebApplication UseBudgetTrackerObservability(this WebApplication app)
    {
        app.UseMiddleware<RequestCorrelationMiddleware>();
        app.UseExceptionHandler();
        app.UseMiddleware<DeniedRequestLoggingMiddleware>();
        app.MapHealthChecks(HealthPath).AllowAnonymous();
        return app;
    }

    /// <summary>Adds the signed-in user's internal UserId to the log scope. Call between <c>UseAuthentication</c> and <c>UseAuthorization</c>.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UseBudgetTrackerRequestUser(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestUserMiddleware>();
}
