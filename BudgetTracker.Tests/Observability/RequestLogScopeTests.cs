using Amazon.Lambda.AspNetCoreServer;
using Amazon.Lambda.Core;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace BudgetTracker.Tests.Observability;

/// <summary>
/// The per-request log scope: every log line written while handling a request carries the correlation ID, the Lambda
/// request ID (when hosted in Lambda) and the internal UserId (when signed in), and never the Cognito sub or email.
/// </summary>
public class RequestLogScopeTests
{
    private const string Sub = "cognito-sub-7f3a";
    private const string LogMessage = "probe";

    [Fact]
    public async Task Should_IncludeCorrelationIdAndLambdaRequestId_InScope()
    {
        var lambda = new Mock<ILambdaContext>();
        lambda.SetupGet(l => l.AwsRequestId).Returns("lambda-req-1");
        await using var host = await StartHostAsync(
            beforePipeline: ctx => ctx.Items[AbstractAspNetCoreFunction.LAMBDA_CONTEXT] = lambda.Object);

        await SendAsync(host, "/anonymous", correlationId: "corr-1");

        var scope = ObservabilityTestHost.ScopeValues(ProbeRecord(host));
        Assert.Equal("corr-1", scope[RequestLogScope.CorrelationIdKey]);
        Assert.Equal("lambda-req-1", scope[RequestLogScope.AwsRequestIdKey]);
    }

    [Fact]
    public async Task Should_OmitLambdaRequestIdAndUserId_When_LocalAndAnonymous()
    {
        await using var host = await StartHostAsync();

        await SendAsync(host, "/anonymous", correlationId: "corr-2");

        var scope = ObservabilityTestHost.ScopeValues(ProbeRecord(host));
        Assert.Equal("corr-2", scope[RequestLogScope.CorrelationIdKey]);
        Assert.False(scope.ContainsKey(RequestLogScope.AwsRequestIdKey));
        Assert.False(scope.ContainsKey(RequestLogScope.UserIdKey));
    }

    [Fact]
    public async Task Should_IncludeInternalUserId_When_Authenticated()
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(u => u.UserId).Returns(42);
        await using var host = await StartHostAsync(currentUser.Object);

        await SendAsync(host, "/signed-in", correlationId: "corr-3", sub: Sub);

        Assert.Equal(42, ObservabilityTestHost.ScopeValues(ProbeRecord(host))[RequestLogScope.UserIdKey]);
    }

    [Fact]
    public async Task Should_StillServeRequestWithoutUserId_When_UserCannotBeResolved()
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(u => u.UserId).Throws(new InvalidOperationException("Failed to provision user"));
        await using var host = await StartHostAsync(currentUser.Object);

        var response = await SendAsync(host, "/signed-in", correlationId: "corr-4", sub: Sub);

        response.EnsureSuccessStatusCode();
        Assert.False(ObservabilityTestHost.ScopeValues(ProbeRecord(host)).ContainsKey(RequestLogScope.UserIdKey));
        var warning = Assert.Single(host.Logs.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Null(warning.Exception);
        Assert.Contains(nameof(InvalidOperationException), ObservabilityTestHost.Render(warning));
        Assert.DoesNotContain("Failed to provision user", ObservabilityTestHost.Render(warning));
    }

    [Fact]
    public async Task Should_NotResolveUser_When_HealthIsCalledWithValidToken()
    {
        var currentUser = new Mock<ICurrentUserProvider>(MockBehavior.Strict);
        await using var host = await StartHostAsync(currentUser.Object);

        var response = await SendAsync(host, ObservabilityExtensions.HealthPath, correlationId: "corr-5", sub: Sub);

        response.EnsureSuccessStatusCode();
        currentUser.VerifyGet(u => u.UserId, Times.Never());
    }

    [Fact]
    public async Task Should_NotResolveUser_When_EndpointAllowsAnonymous()
    {
        var currentUser = new Mock<ICurrentUserProvider>(MockBehavior.Strict);
        await using var host = await StartHostAsync(currentUser.Object);

        var response = await SendAsync(host, "/anonymous", correlationId: "corr-6", sub: Sub);

        response.EnsureSuccessStatusCode();
        currentUser.VerifyGet(u => u.UserId, Times.Never());
    }

    [Fact]
    public async Task Should_NotLogSubEmailOrAuthorizationHeader_ForAnyRecord()
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(u => u.UserId).Returns(42);
        currentUser.SetupGet(u => u.CognitoSub).Returns(Sub);
        currentUser.SetupGet(u => u.Email).Returns(ObservabilityTestHost.TestEmail);
        await using var host = await StartHostAsync(currentUser.Object);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/signed-in");
        request.Headers.Add(ObservabilityTestHost.TestUserHeader, Sub);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer secret-token-value");
        await host.Client.SendAsync(request);

        var records = host.Logs.GetSnapshot();
        Assert.NotEmpty(records);
        foreach (var rendered in records.Select(ObservabilityTestHost.Render))
        {
            Assert.DoesNotContain(Sub, rendered);
            Assert.DoesNotContain(ObservabilityTestHost.TestEmail, rendered);
            Assert.DoesNotContain("secret-token-value", rendered);
        }
    }

    private static FakeLogRecord ProbeRecord(ObservabilityTestHost host) =>
        Assert.Single(host.Logs.GetSnapshot(), r => r.Message == LogMessage);

    private static Task<ObservabilityTestHost> StartHostAsync(
        ICurrentUserProvider? currentUser = null, Action<HttpContext>? beforePipeline = null) =>
        ObservabilityTestHost.StartAsync(
            app =>
            {
                app.MapGet("/anonymous", (ILogger<RequestLogScopeTests> log) => { log.LogInformation(LogMessage); return "ok"; })
                    .AllowAnonymous();
                app.MapGet("/signed-in", (ILogger<RequestLogScopeTests> log) => { log.LogInformation(LogMessage); return "ok"; });
            },
            configure: builder =>
            {
                if (currentUser is not null)
                    builder.Services.AddScoped(_ => currentUser);
            },
            beforePipeline: beforePipeline);

    private static Task<HttpResponseMessage> SendAsync(
        ObservabilityTestHost host, string path, string correlationId, string? sub = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(CorrelationId.HeaderName, correlationId);
        if (sub is not null)
            request.Headers.Add(ObservabilityTestHost.TestUserHeader, sub);
        return host.Client.SendAsync(request);
    }
}
