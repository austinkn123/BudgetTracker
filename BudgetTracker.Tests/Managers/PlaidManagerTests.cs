// [SPIKE] BUD-4 — throwaway. Do not merge to main without replacing SpikePlaidAccessor.

using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Server.Managers;
using Microsoft.Extensions.Logging;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class PlaidManagerTests
{
    private readonly Mock<IPlaidAccessor> _accessorMock;
    private readonly Mock<ILogger<PlaidManager>> _loggerMock;
    private readonly PlaidManager _sut;

    public PlaidManagerTests()
    {
        _accessorMock = new Mock<IPlaidAccessor>();
        _loggerMock = new Mock<ILogger<PlaidManager>>();
        _sut = new PlaidManager(_accessorMock.Object, _loggerMock.Object);
    }

    // ── CreateLinkTokenAsync ────────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnSuccessWithLinkToken_When_AccessorReturnsToken()
    {
        // Arrange
        const int userId = 42;
        const string expectedToken = "link-sandbox-abc123";
        _accessorMock
            .Setup(a => a.CreateLinkTokenAsync(userId))
            .ReturnsAsync(expectedToken);

        // Act
        var result = await _sut.CreateLinkTokenAsync(userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedToken, result.Value);
    }

    [Fact]
    public async Task Should_ReturnFailure_When_AccessorThrowsOnCreateLinkToken()
    {
        // Arrange
        const int userId = 42;
        const string expectedMessage = "Plaid unreachable";
        _accessorMock
            .Setup(a => a.CreateLinkTokenAsync(userId))
            .ThrowsAsync(new HttpRequestException(expectedMessage));

        // Act
        var result = await _sut.CreateLinkTokenAsync(userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedMessage, result.Error);
    }

    // ── ExchangePublicTokenAsync ────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnSuccess_When_AccessorExchangesTokenSuccessfully()
    {
        // Arrange
        const string publicToken = "public-sandbox-abc123";
        _accessorMock
            .Setup(a => a.ExchangePublicTokenAsync(publicToken))
            .ReturnsAsync("access-sandbox-abc123");

        // Act
        var result = await _sut.ExchangePublicTokenAsync(publicToken);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Should_ReturnFailure_When_AccessorThrowsOnExchangeToken()
    {
        // Arrange
        const string publicToken = "public-sandbox-bad";
        const string expectedMessage = "Plaid unreachable";
        _accessorMock
            .Setup(a => a.ExchangePublicTokenAsync(publicToken))
            .ThrowsAsync(new HttpRequestException(expectedMessage));

        // Act
        var result = await _sut.ExchangePublicTokenAsync(publicToken);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedMessage, result.Error);
    }

    [Fact]
    public async Task Should_ReturnFailure_When_PublicTokenIsNullOrEmpty()
    {
        // Act
        var resultNull = await _sut.ExchangePublicTokenAsync(null!);
        var resultEmpty = await _sut.ExchangePublicTokenAsync(string.Empty);

        // Assert
        Assert.False(resultNull.IsSuccess);
        Assert.NotEmpty(resultNull.Error!);
        Assert.False(resultEmpty.IsSuccess);
        Assert.NotEmpty(resultEmpty.Error!);
    }

    [Fact]
    public async Task Should_NeverReturnAccessToken_When_ExchangeSucceeds()
    {
        // Arrange — access_token must be logged only, never surfaced
        const string publicToken = "public-sandbox-abc";
        const string accessToken = "access-sandbox-secret";
        _accessorMock
            .Setup(a => a.ExchangePublicTokenAsync(publicToken))
            .ReturnsAsync(accessToken);

        // Act
        var result = await _sut.ExchangePublicTokenAsync(publicToken);

        // Assert: Result type is non-generic Result — no Value property to leak the token
        Assert.True(result.IsSuccess);
        // The result carries no payload; the access_token is intentionally discarded after logging
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Should_LogWarningWithSpikePrefix_When_ExchangeSucceeds()
    {
        // Arrange
        const string publicToken = "public-sandbox-abc";
        _accessorMock
            .Setup(a => a.ExchangePublicTokenAsync(publicToken))
            .ReturnsAsync("access-sandbox-secret");

        // Act
        await _sut.ExchangePublicTokenAsync(publicToken);

        // Assert: a Warning-level log was emitted (access_token logged with [SPIKE] prefix)
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[SPIKE]")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
