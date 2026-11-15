using System.Net;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BudgetTracker.Tests.Observability;

public class ReadinessEndpointTests
{
    private const string ConnectionSecret = "Host=db.internal;Password=hunter2";

    [Fact]
    public async Task Should_Return401_When_ReadyIsCalledWithoutSignIn()
    {
        var readiness = new Mock<IReadinessManager>(MockBehavior.Strict);
        await using var host = await StartHostAsync(readiness.Object);

        var response = await host.Client.GetAsync(ObservabilityExtensions.ReadyPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        readiness.Verify(r => r.IsReadyAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Should_Return200_When_SignedInAndTheDatabaseAnswers()
    {
        var readiness = new Mock<IReadinessManager>();
        readiness.Setup(r => r.IsReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await using var host = await StartHostAsync(readiness.Object);

        var response = await host.Client.SendAsync(SignedIn(ObservabilityExtensions.ReadyPath));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        readiness.Verify(r => r.IsReadyAsync(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Should_Return503_When_SignedInAndTheDatabaseDoesNotAnswer()
    {
        var readiness = new Mock<IReadinessManager>();
        readiness.Setup(r => r.IsReadyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await using var host = await StartHostAsync(readiness.Object);

        var response = await host.Client.SendAsync(SignedIn(ObservabilityExtensions.ReadyPath));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Should_Return503WithoutLeakingTheFailure_When_TheCheckThrows()
    {
        var readiness = new Mock<IReadinessManager>();
        readiness.Setup(r => r.IsReadyAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(ConnectionSecret));
        await using var host = await StartHostAsync(readiness.Object);

        var response = await host.Client.SendAsync(SignedIn(ObservabilityExtensions.ReadyPath));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain(ConnectionSecret, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(host.Logs.GetSnapshot(), r => ObservabilityTestHost.Render(r).Contains(ConnectionSecret));
    }

    [Fact]
    public async Task Should_NeverRunTheDatabaseCheck_When_HealthIsCalled()
    {
        var readiness = new Mock<IReadinessManager>(MockBehavior.Strict);
        await using var host = await StartHostAsync(readiness.Object);

        var response = await host.Client.SendAsync(SignedIn(ObservabilityExtensions.HealthPath));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        readiness.Verify(r => r.IsReadyAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    private static HttpRequestMessage SignedIn(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(ObservabilityTestHost.TestUserHeader, "cognito-sub-1");
        return request;
    }

    private static Task<ObservabilityTestHost> StartHostAsync(IReadinessManager readiness) =>
        ObservabilityTestHost.StartAsync(_ => { }, builder => builder.Services.AddScoped(_ => readiness));
}
