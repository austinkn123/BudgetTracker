using System.Net;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace BudgetTracker.Tests.Observability;

public class SecurityLoggingTests
{
    private const string Sub = "cognito-sub-9b1c";

    [Fact]
    public async Task Should_LogDeniedRequest_When_Unauthenticated()
    {
        await using var host = await StartHostAsync();

        var response = await host.Client.GetAsync("/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var record = Assert.Single(host.Logs.GetSnapshot(), r => r.Category == SecurityLog.Category);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("401", record.Message);
        Assert.False(ObservabilityTestHost.ScopeValues(record).ContainsKey(RequestLogScope.UserIdKey));
    }

    [Fact]
    public async Task Should_LogDeniedRequestWithUserId_When_Forbidden()
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(u => u.UserId).Returns(7);
        await using var host = await StartHostAsync(b => b.Services.AddScoped(_ => currentUser.Object));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin");
        request.Headers.Add(ObservabilityTestHost.TestUserHeader, Sub);

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var record = Assert.Single(host.Logs.GetSnapshot(), r => r.Category == SecurityLog.Category);
        Assert.Contains("403", record.Message);
        Assert.Equal(7, ObservabilityTestHost.ScopeValues(record)[RequestLogScope.UserIdKey]);
        Assert.DoesNotContain(Sub, ObservabilityTestHost.Render(record));
    }

    [Fact]
    public async Task Should_NotLogSecurityEvent_When_RequestSucceeds()
    {
        await using var host = await StartHostAsync();

        await host.Client.GetAsync("/health");

        Assert.DoesNotContain(host.Logs.GetSnapshot(), r => r.Category == SecurityLog.Category);
    }

    [Fact]
    public async Task Should_LogFailedAuthenticationWithoutTheToken_When_JwtIsInvalid()
    {
        const string token = "not.a-valid.jwt-SECRET-TOKEN-VALUE";
        await using var host = await StartHostAsync(builder =>
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
            {
                o.Configuration = new OpenIdConnectConfiguration();
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = "https://issuer.test",
                    ValidAudience = "audience",
                    IssuerSigningKey = new SymmetricSecurityKey(new byte[32]),
                };
            }));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var security = host.Logs.GetSnapshot().Where(r => r.Category == SecurityLog.Category).ToList();
        Assert.Contains(security, r => r.Message.Contains("Authentication failed"));
        Assert.Contains(security, r => r.Message.Contains("401"));
        Assert.All(host.Logs.GetSnapshot(), r => Assert.DoesNotContain("SECRET-TOKEN-VALUE", ObservabilityTestHost.Render(r)));
    }

    private static Task<ObservabilityTestHost> StartHostAsync(Action<WebApplicationBuilder>? configure = null) =>
        ObservabilityTestHost.StartAsync(
            app =>
            {
                app.MapGet("/protected", () => "secret");
                app.MapGet("/admin", () => "secret").RequireAuthorization(p => p.RequireRole("admin"));
            },
            configure);
}
