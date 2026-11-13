using System.Security.Claims;
using System.Text.Encodings.Web;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Tests.Observability;

/// <summary>
/// An in-memory host wired with the same observability extension methods Program.cs uses, plus a header-driven test
/// authentication scheme and an "authenticated by default" fallback policy (so anonymous access must be explicit).
/// </summary>
internal sealed class ObservabilityTestHost : IAsyncDisposable
{
    public const string TestUserHeader = "X-Test-User";
    public const string TestEmail = "someone@example.com";

    private readonly WebApplication _app;

    private ObservabilityTestHost(WebApplication app)
    {
        _app = app;
        Client = app.GetTestClient();
        Logs = app.Services.GetFakeLogCollector();
    }

    public HttpClient Client { get; }

    public FakeLogCollector Logs { get; }

    public IServiceProvider Services => _app.Services;

    public static async Task<ObservabilityTestHost> StartAsync(
        Action<WebApplication> map,
        Action<WebApplicationBuilder>? configure = null,
        Action<HttpContext>? beforePipeline = null,
        string environment = "Production")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.AddBudgetTrackerObservability();
        builder.Logging.AddFakeLogging();
        builder.Services.AddAuthentication(TestAuthHandler.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
        builder.Services.AddAuthorization(o =>
            o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        configure?.Invoke(builder);

        var app = builder.Build();
        if (beforePipeline is not null)
            app.Use((context, next) => { beforePipeline(context); return next(context); });

        app.UseBudgetTrackerObservability();
        app.UseAuthentication();
        app.UseBudgetTrackerRequestUser();
        app.UseAuthorization();
        map(app);

        await app.StartAsync();
        return new ObservabilityTestHost(app);
    }

    /// <summary>Everything a log record exposes, flattened to text, for "this must never appear" assertions.</summary>
    public static string Render(FakeLogRecord record)
    {
        var parts = new List<string?> { record.Category, record.Message, record.Exception?.ToString() };
        if (record.StructuredState is not null)
            parts.AddRange(record.StructuredState.Select(kv => $"{kv.Key}={kv.Value}"));
        foreach (var scope in record.Scopes)
        {
            parts.Add(scope?.ToString());
            if (scope is IEnumerable<KeyValuePair<string, object?>> items)
                parts.AddRange(items.Select(kv => $"{kv.Key}={kv.Value}"));
        }
        return string.Join('\n', parts);
    }

    /// <summary>The scope values visible to <paramref name="record"/>, last writer wins.</summary>
    public static IReadOnlyDictionary<string, object?> ScopeValues(FakeLogRecord record)
    {
        var values = new Dictionary<string, object?>();
        foreach (var scope in record.Scopes.OfType<IEnumerable<KeyValuePair<string, object?>>>())
            foreach (var (key, value) in scope)
                values[key] = value;
        return values;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }

    /// <summary>Authenticates when <see cref="TestUserHeader"/> is present, as a user whose sub is the header value.</summary>
    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Scheme = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(TestUserHeader, out var sub))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
                [new Claim("sub", sub.ToString()), new Claim("email", TestEmail)], Scheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
