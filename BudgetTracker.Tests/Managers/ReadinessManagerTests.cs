using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class ReadinessManagerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Should_ReportWhatTheDatabaseAccessorReports(bool canConnect)
    {
        using var cancellation = new CancellationTokenSource();
        var accessor = new Mock<IDatabaseHealthAccessor>();
        accessor.Setup(a => a.CanConnectAsync(cancellation.Token)).ReturnsAsync(canConnect);

        var ready = await new ReadinessManager(accessor.Object).IsReadyAsync(cancellation.Token);

        Assert.Equal(canConnect, ready);
        accessor.Verify(a => a.CanConnectAsync(cancellation.Token), Times.Once());
    }
}
