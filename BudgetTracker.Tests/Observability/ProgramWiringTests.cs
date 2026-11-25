using System.Net;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Server.Managers;
using BudgetTracker.Server.Services;
using BudgetTracker.Server.Utilities;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;

namespace BudgetTracker.Tests.Observability;

/// <summary>
/// Smoke tests of the real Program.cs pipeline. The database, Cognito and the timer-driven hosted services are
/// replaced, so nothing here leaves the process: a fake connection string, a header-driven test auth scheme and a
/// stubbed <see cref="ICurrentUserProvider"/>.
/// </summary>
public class ProgramWiringTests
{
    private const string AdminClaim = "test-admin";
    private const string OriginSecret = "test-origin-secret";

    [Fact]
    public async Task Should_ServeHealthAndEchoCorrelationId_When_ProgramIsHosted()
    {
        var currentUser = new Mock<ICurrentUserProvider>(MockBehavior.Strict);
        await using var factory = CreateFactory(currentUser.Object);
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, ObservabilityExtensions.HealthPath);
        request.Headers.Add(CorrelationId.HeaderName, "wiring-1");
        request.Headers.Add(ObservabilityTestHost.TestUserHeader, "cognito-sub-1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("wiring-1", Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)));
        currentUser.VerifyGet(u => u.UserId, Times.Never());
    }

    [Fact]
    public async Task Should_RequireSignInForReadiness_When_ProgramIsHosted()
    {
        var currentUser = new Mock<ICurrentUserProvider>(MockBehavior.Strict);
        await using var factory = CreateFactory(currentUser.Object);
        using var client = CreateClient(factory);

        var response = await client.GetAsync(ObservabilityExtensions.ReadyPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Should_ResolveTheReadinessChain_When_ProgramIsHosted()
    {
        await using var factory = CreateFactory(new Mock<ICurrentUserProvider>().Object);
        using var scope = factory.Services.CreateScope();

        // Managers and Accessors are registered by name scan; this fails if either new class falls outside it.
        Assert.IsType<ReadinessManager>(scope.ServiceProvider.GetRequiredService<IReadinessManager>());
    }

    [Fact]
    public async Task Should_ResolveThePayeeRuleChain_When_ProgramIsHosted()
    {
        await using var factory = CreateFactory(new Mock<ICurrentUserProvider>().Object);
        using var scope = factory.Services.CreateScope();

        // BUD-24 added CategoryRule{Engine,Accessor,Manager} and new dependencies on the category and Plaid managers.
        Assert.IsType<BudgetTracker.Server.Managers.CategoryRuleManager>(
            scope.ServiceProvider.GetRequiredService<BudgetTracker.Domain.Interfaces.Managers.ICategoryRuleManager>());
        Assert.IsType<BudgetTracker.Server.Managers.CategoryManager>(
            scope.ServiceProvider.GetRequiredService<BudgetTracker.Domain.Interfaces.Managers.ICategoryManager>());
        Assert.IsType<BudgetTracker.Server.Managers.PlaidManager>(
            scope.ServiceProvider.GetRequiredService<BudgetTracker.Domain.Interfaces.Managers.IPlaidManager>());
        Assert.IsType<BudgetTracker.Server.Managers.TransactionExportManager>(
            scope.ServiceProvider.GetRequiredService<BudgetTracker.Domain.Interfaces.Managers.ITransactionExportManager>());
    }

    [Fact]
    public async Task Should_AttachTheSlowQueryInterceptorToTheDbContext_When_ProgramIsHosted()
    {
        await using var factory = CreateFactory(new Mock<ICurrentUserProvider>().Object);
        using var scope = factory.Services.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<BudgetTrackerDbContext>>();

        var interceptors = options.FindExtension<CoreOptionsExtension>()?.Interceptors ?? [];
        Assert.Contains(interceptors, i => i is SlowQueryInterceptor);
    }

    [Fact]
    public async Task Should_ResolveUserBetweenAuthenticationAndAuthorization_When_ProgramIsHosted()
    {
        // Authenticated (so RequestUserMiddleware runs after UseAuthentication) but denied by the default policy (so a
        // UserId on the 403 line proves it ran before UseAuthorization short-circuited).
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(u => u.UserId).Returns(42);
        await using var factory = CreateFactory(currentUser.Object);
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Add(ObservabilityTestHost.TestUserHeader, "cognito-sub-1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var denied = Assert.Single(factory.Services.GetFakeLogCollector().GetSnapshot(), r => r.Category == SecurityLog.Category);
        Assert.Equal(42, ObservabilityTestHost.ScopeValues(denied)[RequestLogScope.UserIdKey]);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/")]
    [InlineData("/api/categories")]
    public async Task Should_Return403_When_OriginSecretIsSetAndHeaderIsMissing(string path)
    {
        await using var factory = CreateFactory(new Mock<ICurrentUserProvider>().Object, OriginSecret);
        using var client = CreateClient(factory);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Should_Return403_When_OriginHeaderIsWrong()
    {
        await using var factory = CreateFactory(new Mock<ICurrentUserProvider>().Object, OriginSecret);
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, ObservabilityExtensions.HealthPath);
        request.Headers.Add(CloudflareOriginGuard.HeaderName, OriginSecret + "x");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Should_ServeHealth_When_OriginHeaderMatches()
    {
        await using var factory = CreateFactory(new Mock<ICurrentUserProvider>().Object, OriginSecret);
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, ObservabilityExtensions.HealthPath);
        request.Headers.Add(CloudflareOriginGuard.HeaderName, OriginSecret);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(ICurrentUserProvider currentUser, string? originSecret = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("ConnectionStrings:BudgetTrackerConnection", "Host=unused.invalid;Database=unused");
            if (originSecret is not null)
                builder.UseSetting(CloudflareOriginGuard.ConfigKey, originSecret);
            builder.ConfigureTestServices(services =>
            {
                // Both tick against the database; the real host only runs them outside Lambda.
                services.RemoveAll<IHostedService>(d =>
                    d.ImplementationType == typeof(PlaidSyncSweepService) || d.ImplementationType == typeof(WebullCloseSnapshotService));
                services.AddAuthentication(ObservabilityTestHost.TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, ObservabilityTestHost.TestAuthHandler>(
                        ObservabilityTestHost.TestAuthHandler.SchemeName, _ => { });
                services.Configure<AuthorizationOptions>(o =>
                    o.DefaultPolicy = new AuthorizationPolicyBuilder().RequireClaim(AdminClaim).Build());
                services.AddScoped(_ => currentUser);
                services.AddLogging(logging => logging.AddFakeLogging());
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
}

file static class ServiceCollectionTestExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services, Func<ServiceDescriptor, bool> match)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(T) && match(d)).ToList())
            services.Remove(descriptor);
    }
}
