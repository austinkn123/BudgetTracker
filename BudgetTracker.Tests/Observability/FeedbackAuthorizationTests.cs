using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Endpoints;
using BudgetTracker.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace BudgetTracker.Tests.Observability;

/// <summary>
/// The real Program.cs pipeline for <c>/api/feedback</c>: any signed-in user may send feedback; only members of the
/// Cognito "admin" group may read it. Cognito is replaced by a header-driven scheme that sets <c>cognito:groups</c>.
/// </summary>
public class FeedbackAuthorizationTests
{
    private const string UserHeader = "X-Test-Sub";
    private const string GroupsHeader = "X-Test-Groups";

    private readonly Mock<IFeedbackManager> _manager = new();

    public FeedbackAuthorizationTests()
    {
        _manager.Setup(m => m.SubmitAsync(It.IsAny<Feedback>())).ReturnsAsync(Result<int>.Success(1));
        _manager.Setup(m => m.GetPageAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Result<IReadOnlyList<FeedbackView>>.Success([]));
    }

    [Fact]
    public async Task Should_Return401_When_SignedOut()
    {
        using var client = CreateClient(out var factory);
        await using var _ = factory;

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/feedback", new { message = "hi" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/feedback")).StatusCode);
    }

    [Fact]
    public async Task Should_LetAnySignedInUserSubmit()
    {
        using var client = CreateClient(out var factory);
        await using var _ = factory;

        var response = await client.SendAsync(Request(HttpMethod.Post, groups: null, JsonContent.Create(new { message = "hi", page = "/" })));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("admins")]
    [InlineData("Admin")]
    [InlineData("beta")]
    public async Task Should_Return403OnRead_When_NotInTheAdminGroup(string? groups)
    {
        using var client = CreateClient(out var factory);
        await using var _ = factory;

        var response = await client.SendAsync(Request(HttpMethod.Get, groups));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _manager.Verify(m => m.GetPageAsync(It.IsAny<int?>(), It.IsAny<int?>()), Times.Never());
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("beta,admin")]
    public async Task Should_ServeTheRead_When_InTheAdminGroup(string groups)
    {
        using var client = CreateClient(out var factory);
        await using var _ = factory;

        var response = await client.SendAsync(Request(HttpMethod.Get, groups));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReadCognitoGroupsArrayAsOneClaimPerGroup()
    {
        // Cognito puts groups in the ID token as a JSON array; the admin policy relies on each becoming its own claim.
        var key = new SymmetricSecurityKey(new byte[32]);
        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object> { ["sub"] = "s", [FeedbackEndpoints.GroupsClaim] = new[] { "beta", "admin" } },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });

        var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            IssuerSigningKey = key,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
        });

        Assert.True(result.IsValid);
        Assert.Contains(result.ClaimsIdentity.FindAll(FeedbackEndpoints.GroupsClaim), c => c.Value == FeedbackEndpoints.AdminGroup);
    }

    private static HttpRequestMessage Request(HttpMethod method, string? groups, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, "/api/feedback") { Content = content };
        request.Headers.Add(UserHeader, "cognito-sub-1");
        if (groups is not null)
            request.Headers.Add(GroupsHeader, groups);
        return request;
    }

    private HttpClient CreateClient(out WebApplicationFactory<Program> factory)
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(u => u.UserId).Returns(5);
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("ConnectionStrings:BudgetTrackerConnection", "Host=unused.invalid;Database=unused");
            builder.ConfigureTestServices(services =>
            {
                foreach (var d in services.Where(d => d.ServiceType == typeof(IHostedService) &&
                             (d.ImplementationType == typeof(PlaidSyncSweepService) || d.ImplementationType == typeof(WebullCloseSnapshotService))).ToList())
                    services.Remove(d);
                services.AddAuthentication(GroupsAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, GroupsAuthHandler>(GroupsAuthHandler.SchemeName, _ => { });
                services.AddScoped(_ => currentUser.Object);
                services.AddScoped(_ => _manager.Object);
            });
        });
        return factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    private sealed class GroupsAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestGroups";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var sub))
                return Task.FromResult(AuthenticateResult.NoResult());

            List<Claim> claims = [new("sub", sub.ToString()), new("email", "user@example.com")];
            if (Request.Headers.TryGetValue(GroupsHeader, out var groups))
                claims.AddRange(groups.ToString().Split(',').Select(g => new Claim(FeedbackEndpoints.GroupsClaim, g)));

            var identity = new ClaimsIdentity(claims, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
