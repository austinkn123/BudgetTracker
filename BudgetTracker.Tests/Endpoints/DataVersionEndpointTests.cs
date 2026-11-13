using System.Security.Claims;
using System.Text.Json;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Server.Endpoints;
using BudgetTracker.Server.Managers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BudgetTracker.Tests.Endpoints;

/// <summary>
/// <c>GET /api/data-version</c>, run through <see cref="RequestDelegateFactory"/> so parameter binding is the real one: the
/// handler must not resolve <see cref="ICurrentUserProvider"/> (which provisions a user on first touch), so the 20-second
/// poll never creates rows. A throwing registration proves it is never asked for.
/// </summary>
public class DataVersionEndpointTests
{
    private readonly Mock<IDataVersionAccessor> _accessor = new(MockBehavior.Strict);
    private readonly Mock<IUserManager> _userManager = new(MockBehavior.Strict);
    private readonly Mock<IUserAccessor> _userAccessor = new(MockBehavior.Strict);
    private int _currentUserResolutions;

    private IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton(_accessor.Object);
        services.AddSingleton(_userManager.Object);
        services.AddSingleton(_userAccessor.Object);
        services.AddScoped<IDataVersionManager, DataVersionManager>();
        services.AddScoped<ICurrentUserProvider>(_ =>
        {
            _currentUserResolutions++;
            throw new InvalidOperationException("ICurrentUserProvider must not be resolved by the data-version poll");
        });
        return services.BuildServiceProvider();
    }

    private async Task<HttpContext> InvokeAsync(params Claim[] claims)
    {
        var services = BuildServices();
        var handler = RequestDelegateFactory.Create(
            DataVersionEndpoints.GetAsync,
            new RequestDelegateFactoryOptions { ServiceProvider = services });

        var context = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, claims.Length > 0 ? "Bearer" : null))
        };
        context.Response.Body = new MemoryStream();

        await handler.RequestDelegate(context);
        return context;
    }

    private static JsonElement BodyOf(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body).RootElement.Clone();
    }

    [Fact]
    public async Task Should_Return200WithVersionBody_When_TheUserExists()
    {
        _accessor.Setup(a => a.GetByCognitoSubAsync("sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(42L);

        var context = await InvokeAsync(new Claim("sub", "sub-1"));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var body = BodyOf(context);
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        Assert.Equal(42L, body.GetProperty("version").GetInt64());
        Assert.Single(body.EnumerateObject());
    }

    [Fact]
    public async Task Should_SetCacheControlNoStore()
    {
        _accessor.Setup(a => a.GetByCognitoSubAsync("sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(3L);

        var context = await InvokeAsync(new Claim("sub", "sub-1"));

        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Should_ReturnZeroWithoutCreatingAUser_When_TheSubIsUnknown()
    {
        _accessor.Setup(a => a.GetByCognitoSubAsync("new-sub", It.IsAny<CancellationToken>())).ReturnsAsync((long?)null);

        var context = await InvokeAsync(new Claim("sub", "new-sub"));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0L, BodyOf(context).GetProperty("version").GetInt64());
        _userManager.VerifyNoOtherCalls();
        _userAccessor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Should_Return401_When_TheSubClaimIsMissing()
    {
        var context = await InvokeAsync(new Claim("email", "a@b.c"));

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        _accessor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Should_Return401_When_TheRequestIsAnonymous()
    {
        var context = await InvokeAsync();

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Should_ReadTheRawSubClaim_NotTheMappedNameIdentifier()
    {
        // MapInboundClaims is off in Program.cs, so the token's "sub" arrives as "sub" (as CognitoCurrentUserProvider reads it).
        var context = await InvokeAsync(new Claim(ClaimTypes.NameIdentifier, "sub-1"));

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Should_NeverResolveTheCurrentUserProvider()
    {
        _accessor.Setup(a => a.GetByCognitoSubAsync("sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(1L);

        var context = await InvokeAsync(new Claim("sub", "sub-1"));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, _currentUserResolutions);
    }
}
