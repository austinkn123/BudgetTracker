using System.Net;
using System.Text.Json;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

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
    public async Task Should_LogUnhandledExceptionOnce_WithCorrelationIdInScope_When_EndpointThrows()
    {
        await using var host = await StartThrowingHostAsync();

        await SendBoomAsync(host, "corr-500");

        var record = Assert.Single(host.Logs.GetSnapshot(), r => r.Exception is not null);
        Assert.Equal("corr-500", ObservabilityTestHost.ScopeValues(record)[RequestLogScope.CorrelationIdKey]);
    }

    private static Task<ObservabilityTestHost> StartThrowingHostAsync() =>
        ObservabilityTestHost.StartAsync(app =>
            app.MapGet("/boom", string () => throw new InvalidOperationException(SecretMessage)).AllowAnonymous());

    private static Task<HttpResponseMessage> SendBoomAsync(ObservabilityTestHost host, string correlationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/boom");
        request.Headers.Add(CorrelationId.HeaderName, correlationId);
        return host.Client.SendAsync(request);
    }
}
