using System.Net;
using System.Text.Json;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BudgetTracker.Tests.Observability;

public class ExceptionHandlingTests
{
    private const string SecretMessage = "payee Acme Corp amount 123.45";

    [Fact]
    public async Task Should_ReturnProblemDetailsWithCorrelationId_When_EndpointThrows()
    {
        await using var host = await StartThrowingHostAsync();

        var response = await SendBoomAsync(host, "corr-500");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(500, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("corr-500", body.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("corr-500", Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)));
    }

    [Fact]
    public async Task Should_NotExposeExceptionMessageOrStackTrace_When_EndpointThrows()
    {
        await using var host = await StartThrowingHostAsync();

        var response = await SendBoomAsync(host, "corr-500");

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretMessage, raw);
        Assert.DoesNotContain(nameof(InvalidOperationException), raw);
        Assert.DoesNotContain(" at ", raw);
        using var body = JsonDocument.Parse(raw);
        Assert.False(body.RootElement.TryGetProperty("exception", out _));
        Assert.False(body.RootElement.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public async Task Should_LogUnhandledExceptionOnce_WithTypeAndCorrelationIdInScope_When_EndpointThrows()
    {
        await using var host = await StartThrowingHostAsync();

        await SendBoomAsync(host, "corr-500");

        var record = Assert.Single(host.Logs.GetSnapshot(), r => r.Level >= LogLevel.Error);
        Assert.Contains(nameof(InvalidOperationException), ObservabilityTestHost.Render(record));
        Assert.Contains(" at ", ObservabilityTestHost.Render(record));
        Assert.Equal("corr-500", ObservabilityTestHost.ScopeValues(record)[RequestLogScope.CorrelationIdKey]);
    }

    [Fact]
    public async Task Should_NotLogExceptionMessage_When_EndpointThrows()
    {
        await using var host = await StartThrowingHostAsync();

        var response = await SendBoomAsync(host, "corr-500");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.All(host.Logs.GetSnapshot(), r => Assert.DoesNotContain(SecretMessage, ObservabilityTestHost.Render(r)));
    }

    [Fact]
    public async Task Should_NotLogInnerExceptionMessage_When_EndpointThrowsWrappedException()
    {
        await using var host = await StartThrowingHostAsync();

        await host.Client.GetAsync("/wrapped");

        var record = Assert.Single(host.Logs.GetSnapshot(), r => r.Level >= LogLevel.Error);
        Assert.Contains(nameof(ArgumentException), ObservabilityTestHost.Render(record));
        Assert.All(host.Logs.GetSnapshot(), r => Assert.DoesNotContain(SecretMessage, ObservabilityTestHost.Render(r)));
    }

    [Fact]
    public async Task Should_NotWriteBuiltInExceptionHandlerDiagnostic_When_EndpointThrows()
    {
        await using var host = await StartThrowingHostAsync();

        await SendBoomAsync(host, "corr-500");

        Assert.DoesNotContain(host.Logs.GetSnapshot(), r => r.Category == BuiltInExceptionHandlerCategory);
    }

    [Fact]
    public async Task Should_LogUnhandledExceptionWithUserId_When_AuthenticatedEndpointThrows()
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(u => u.UserId).Returns(42);
        await using var host = await StartThrowingHostAsync(b => b.Services.AddScoped(_ => currentUser.Object));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/signed-in-boom");
        request.Headers.Add(ObservabilityTestHost.TestUserHeader, "cognito-sub-1");

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var record = Assert.Single(host.Logs.GetSnapshot(), r => r.Level >= LogLevel.Error);
        Assert.Equal(42, ObservabilityTestHost.ScopeValues(record)[RequestLogScope.UserIdKey]);
    }

    private const string BuiltInExceptionHandlerCategory = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";

    private static Task<ObservabilityTestHost> StartThrowingHostAsync(Action<WebApplicationBuilder>? configure = null) =>
        ObservabilityTestHost.StartAsync(
            app =>
            {
                app.MapGet("/boom", string () => throw new InvalidOperationException(SecretMessage)).AllowAnonymous();
                app.MapGet("/wrapped", string () =>
                    throw new InvalidOperationException(SecretMessage, new ArgumentException(SecretMessage))).AllowAnonymous();
                app.MapGet("/signed-in-boom", string () => throw new InvalidOperationException(SecretMessage));
            },
            configure);

    private static Task<HttpResponseMessage> SendBoomAsync(ObservabilityTestHost host, string correlationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/boom");
        request.Headers.Add(CorrelationId.HeaderName, correlationId);
        return host.Client.SendAsync(request);
    }
}
