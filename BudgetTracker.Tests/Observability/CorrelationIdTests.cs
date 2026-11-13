using System.Net;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BudgetTracker.Tests.Observability;

public class CorrelationIdTests
{
    [Theory]
    [InlineData("abc-123")]
    [InlineData("A1b2.C3_d4-e5")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")] // 64 chars: the maximum
    public void Should_AcceptCorrelationId_When_ShortAndSafe(string value) =>
        Assert.True(CorrelationId.IsValid(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("new\nline")]
    [InlineData("quote\"")]
    [InlineData("<script>")]
    [InlineData("caf\u00e9")]
    [InlineData("a,a")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0")] // 65 chars
    public void Should_RejectCorrelationId_When_MissingUnsafeOrOversized(string? value) =>
        Assert.False(CorrelationId.IsValid(value));

    [Fact]
    public void Should_GenerateValidCorrelationId()
    {
        var first = CorrelationId.New();

        Assert.True(CorrelationId.IsValid(first));
        Assert.NotEqual(first, CorrelationId.New());
    }

    [Fact]
    public async Task Should_EchoInboundCorrelationId_When_Valid()
    {
        await using var host = await StartEchoHostAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/echo");
        request.Headers.Add(CorrelationId.HeaderName, "client-abc-123");

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("client-abc-123", Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)));
        Assert.Equal("client-abc-123", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("caf\u00e9")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef-oversized")]
    public async Task Should_ReplaceCorrelationId_When_InboundIsInvalid(string inbound)
    {
        await using var host = await StartEchoHostAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/echo");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, inbound);

        var response = await host.Client.SendAsync(request);

        var echoed = Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));
        Assert.NotEqual(inbound, echoed);
        Assert.True(CorrelationId.IsValid(echoed));
        Assert.Equal(echoed, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Should_ReplaceCorrelationId_When_HeaderIsRepeated()
    {
        await using var host = await StartEchoHostAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/echo");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, ["a", "a"]);

        var response = await host.Client.SendAsync(request);

        var echoed = Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));
        Assert.NotEqual("a", echoed);
        Assert.True(CorrelationId.IsValid(echoed));
    }

    [Fact]
    public async Task Should_GenerateCorrelationId_When_HeaderMissing()
    {
        await using var host = await StartEchoHostAsync();

        var response = await host.Client.GetAsync("/echo");

        var echoed = Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName));
        Assert.True(CorrelationId.IsValid(echoed));
        Assert.Equal(echoed, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Should_EchoCorrelationId_When_RequestIsDenied()
    {
        await using var host = await StartEchoHostAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Add(CorrelationId.HeaderName, "denied-1");

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("denied-1", Assert.Single(response.Headers.GetValues(CorrelationId.HeaderName)));
    }

    private static Task<ObservabilityTestHost> StartEchoHostAsync() =>
        ObservabilityTestHost.StartAsync(app =>
        {
            app.MapGet("/echo", (HttpContext context) => CorrelationId.Get(context)).AllowAnonymous();
            app.MapGet("/protected", () => "secret");
        });
}
