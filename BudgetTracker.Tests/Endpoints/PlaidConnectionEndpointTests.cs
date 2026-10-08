using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Server.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace BudgetTracker.Tests.Endpoints;

/// <summary>
/// Handler-level tests for the multi-connection Plaid routes (<c>GET /connections</c>,
/// <c>DELETE /connections/{id}</c>, <c>POST /sync?staleAfterHours=</c>). Same approach as
/// <see cref="PlaidWebhookEndpointTests"/>: handlers are public statics exercised without a TestServer,
/// pinning the HTTP contract the client is built against (status codes + body shape).
/// </summary>
public class PlaidConnectionEndpointTests
{
    private readonly Mock<IPlaidManager> _manager = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUserProvider> _currentUser = new();

    public PlaidConnectionEndpointTests()
    {
        _currentUser.SetupGet(u => u.UserId).Returns(5);
    }

    private static int StatusOf(IResult result) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode!.Value;

    // ── GET /connections ────────────────────────────────────────────────────

    [Fact]
    public async Task GetConnections_ReturnsOkWithEveryActiveConnection()
    {
        IReadOnlyList<PlaidConnectionView> views =
        [
            new(11, "Chase", null, []),
            new(12, "Wells Fargo", new DateTime(2026, 9, 1), [new PlaidLinkedAccountView("wf-sav", "Savings", "9999", "depository")])
        ];
        _manager.Setup(m => m.GetConnectionsAsync(5)).ReturnsAsync(views);

        var result = await PlaidEndpoints.GetConnectionsAsync(_manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status200OK, StatusOf(result));
        var body = Assert.IsType<Ok<IReadOnlyList<PlaidConnectionView>>>(result).Value;
        Assert.Equal([11, 12], body!.Select(v => v.PlaidItemId));
    }

    [Fact]
    public async Task GetConnections_NoneLinked_ReturnsOkWithEmptyArray()
    {
        _manager.Setup(m => m.GetConnectionsAsync(5)).ReturnsAsync([]);

        var result = await PlaidEndpoints.GetConnectionsAsync(_manager.Object, _currentUser.Object);

        // Empty array, never 404 — the client renders "Connect a bank" off an empty list.
        Assert.Equal(StatusCodes.Status200OK, StatusOf(result));
        Assert.Empty(Assert.IsType<Ok<IReadOnlyList<PlaidConnectionView>>>(result).Value!);
    }

    // ── DELETE /connections/{plaidItemId} ───────────────────────────────────

    [Fact]
    public async Task DeleteConnection_Success_Returns204()
    {
        _manager.Setup(m => m.DisconnectAsync(5, 11)).ReturnsAsync(Result.Success());

        var result = await PlaidEndpoints.DeleteConnectionAsync(11, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status204NoContent, StatusOf(result));
        _manager.Verify(m => m.DisconnectAsync(5, 11), Times.Once);
    }

    [Fact]
    public async Task DeleteConnection_NotFoundNotOwnedOrInactive_Returns404()
    {
        _manager.Setup(m => m.DisconnectAsync(5, 99)).ReturnsAsync(Result.Failure("Bank connection not found"));

        var result = await PlaidEndpoints.DeleteConnectionAsync(99, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(result));
    }

    // ── POST /sync?staleAfterHours= ─────────────────────────────────────────

    [Fact]
    public async Task Sync_PassesStaleAfterHoursAndReturnsOkWithSummary()
    {
        var summary = new PlaidSyncSummary(3, 1, 0, new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc));
        _manager.Setup(m => m.SyncAsync(5, 6)).ReturnsAsync(Result<PlaidSyncSummary>.Success(summary));

        var result = await PlaidEndpoints.SyncAsync(6, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status200OK, StatusOf(result));
        Assert.Equal(summary, Assert.IsType<Ok<PlaidSyncSummary>>(result).Value);
    }

    [Fact]
    public async Task Sync_NoStaleAfterHours_SyncsEverything()
    {
        var summary = new PlaidSyncSummary(0, 0, 0, DateTime.UtcNow);
        _manager.Setup(m => m.SyncAsync(5, null)).ReturnsAsync(Result<PlaidSyncSummary>.Success(summary));

        var result = await PlaidEndpoints.SyncAsync(null, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status200OK, StatusOf(result));
        _manager.Verify(m => m.SyncAsync(5, null), Times.Once);
    }

    [Fact]
    public async Task Sync_ManagerFailure_Returns400()
    {
        _manager.Setup(m => m.SyncAsync(5, -1))
            .ReturnsAsync(Result<PlaidSyncSummary>.Failure("staleAfterHours cannot be negative"));

        var result = await PlaidEndpoints.SyncAsync(-1, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(result));
    }

    [Fact]
    public async Task Sync_ManagerFailure_BodyIsErrorObject()
    {
        // The client reads { error } off every 400, so a bare string body would surface as "undefined".
        const string reconnect = "A bank connection needs to be reconnected. Disconnect it and link it again.";
        _manager.Setup(m => m.SyncAsync(5, null)).ReturnsAsync(Result<PlaidSyncSummary>.Failure(reconnect));

        var result = await PlaidEndpoints.SyncAsync(null, _manager.Object, _currentUser.Object);

        Assert.Equal(reconnect, EndpointAssert.ErrorOf(result));
    }
}
