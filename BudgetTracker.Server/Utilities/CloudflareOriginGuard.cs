using System.Security.Cryptography;
using System.Text;

namespace BudgetTracker.Server.Utilities;

/// <summary>
/// Locks the Lambda Function URL to the Cloudflare Worker (deploy/cloudflare/worker.js): the Worker adds
/// <see cref="HeaderName"/> with a shared secret, and any request without it gets 403. Off when
/// <see cref="ConfigKey"/> is unset (local dev, tests).
/// </summary>
public static class CloudflareOriginGuard
{
    /// <summary>The header the Worker sets.</summary>
    public const string HeaderName = "X-Origin-Auth";

    /// <summary>The shared secret; SSM <c>/budgettracker/Cloudflare/OriginSecret</c> in production.</summary>
    public const string ConfigKey = "Cloudflare:OriginSecret";

    /// <summary>Rejects requests without the origin secret. Call before static files, /health and endpoints.</summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    public static WebApplication UseCloudflareOriginGuard(this WebApplication app)
    {
        var secret = app.Configuration[ConfigKey];
        if (string.IsNullOrEmpty(secret))
            return app;

        // Hashing both sides gives equal-length inputs, so the fixed-time compare doesn't leak the secret's length.
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        app.Use((context, next) =>
        {
            var given = SHA256.HashData(Encoding.UTF8.GetBytes(context.Request.Headers[HeaderName].ToString()));
            if (CryptographicOperations.FixedTimeEquals(given, expected))
                return next(context);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        });
        return app;
    }
}
