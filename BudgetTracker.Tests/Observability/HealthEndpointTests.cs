using System.Net;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Tests.Observability;

public class HealthEndpointTests
{
    [Fact]
    public async Task Should_Return200_When_HealthIsCalledWithoutSignIn()
    {
        await using var host = await StartHostAsync();

        var response = await host.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Should_StillRequireSignIn_ForOtherEndpoints()
    {
        await using var host = await StartHostAsync();

        var response = await host.Client.GetAsync("/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Should_TagEveryRegisteredCheckAsReadyOnly_SoHealthNeverCallsTheDatabase()
    {
        await using var host = await StartHostAsync();

        var options = host.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        // /health runs no checks at all; anything registered must be opt-in through the readiness tag.
        Assert.All(options.Registrations, r => Assert.Contains(ObservabilityExtensions.ReadyTag, r.Tags));
    }

    private static Task<ObservabilityTestHost> StartHostAsync() =>
        ObservabilityTestHost.StartAsync(app => app.MapGet("/protected", () => "secret"));
}
