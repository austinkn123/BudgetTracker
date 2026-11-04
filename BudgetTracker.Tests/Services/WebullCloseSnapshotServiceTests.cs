using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BudgetTracker.Tests.Services;

/// <summary>
/// <see cref="WebullCloseSnapshotService"/> ticks once at startup (the catch-up after the machine was off), in a fresh DI
/// scope, and a failed tick is logged without stopping the service.
/// </summary>
public class WebullCloseSnapshotServiceTests
{
    [Fact]
    public async Task Startup_RunsOnceImmediately_AndAFailedTickIsLoggedWithoutStoppingTheService()
    {
        var failure = new InvalidOperationException("database unavailable");
        var ran = new TaskCompletionSource();
        var manager = new Mock<IWebullManager>();
        manager.Setup(m => m.RunCloseSnapshotsAsync()).Callback(() => ran.TrySetResult()).ThrowsAsync(failure);
        var logger = new Mock<ILogger<WebullCloseSnapshotService>>();
        var services = new ServiceCollection().AddScoped(_ => manager.Object).BuildServiceProvider();
        using var service = new WebullCloseSnapshotService(services.GetRequiredService<IServiceScopeFactory>(), logger.Object);

        await service.StartAsync(CancellationToken.None);
        await ran.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50); // let the catch block run

        manager.Verify(m => m.RunCloseSnapshotsAsync(), Times.Once);
        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            failure,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once());
        Assert.False(service.ExecuteTask!.IsCompleted); // still waiting for the next 10-minute tick

        await service.StopAsync(CancellationToken.None);
    }
}
