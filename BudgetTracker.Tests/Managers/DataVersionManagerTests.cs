using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class DataVersionManagerTests
{
    private readonly Mock<IDataVersionAccessor> _accessor = new(MockBehavior.Strict);

    private DataVersionManager BuildSut() => new(_accessor.Object);

    [Fact]
    public async Task Should_ReturnTheStoredVersion_When_TheUserExists()
    {
        _accessor.Setup(a => a.GetByCognitoSubAsync("sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(42L);

        var version = await BuildSut().GetVersionAsync("sub-1");

        Assert.Equal(42L, version);
    }

    [Fact]
    public async Task Should_ReturnZero_When_TheUserIsNotProvisioned()
    {
        _accessor.Setup(a => a.GetByCognitoSubAsync("unknown", It.IsAny<CancellationToken>())).ReturnsAsync((long?)null);

        var version = await BuildSut().GetVersionAsync("unknown");

        Assert.Equal(0L, version);
    }

    [Fact]
    public async Task Should_PassTheCancellationTokenToTheAccessor()
    {
        using var cts = new CancellationTokenSource();
        _accessor.Setup(a => a.GetByCognitoSubAsync("sub-1", cts.Token)).ReturnsAsync(7L);

        var version = await BuildSut().GetVersionAsync("sub-1", cts.Token);

        Assert.Equal(7L, version);
        _accessor.VerifyAll();
    }
}
