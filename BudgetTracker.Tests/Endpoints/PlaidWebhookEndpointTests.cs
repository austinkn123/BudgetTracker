using System.Text;
using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Server.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace BudgetTracker.Tests.Endpoints;

/// <summary>
/// Handler-level tests for <c>POST /api/plaid/webhook</c>. No WebApplicationFactory/TestServer harness
/// exists in this repo (the suite is pure unit tests) and standing one up would drag in Cognito auth,
/// the SQL Server DbContext, and Data Protection — disproportionate for one endpoint. Per johnny's
/// guidance, the handler logic was extracted to <see cref="PlaidEndpoints.HandleWebhookAsync"/> and is
/// exercised here with a fabricated <see cref="DefaultHttpContext"/>. These pin the real attacker surface:
/// the body is read verbatim, the header is forwarded, and the response is ALWAYS a neutral 200.
/// </summary>
public class PlaidWebhookEndpointTests
{
    private readonly Mock<IPlaidManager> _manager = new(MockBehavior.Strict);

    /// <summary>Builds an HttpRequest with the given raw body and optional Plaid-Verification header value(s).</summary>
    private static HttpRequest BuildRequest(string body, params string[] verificationHeaderValues)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (verificationHeaderValues.Length > 0)
            context.Request.Headers["Plaid-Verification"] = verificationHeaderValues;
        return context.Request;
    }

    /// <summary>Asserts the result is a neutral 200 without needing a service provider to execute it.</summary>
    private static void AssertOk(IResult result)
    {
        // Results.Ok() is an Ok result exposing StatusCode via IStatusCodeHttpResult.
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, status.StatusCode);
    }

    [Fact]
    public async Task Webhook_ValidRequest_Returns200AndForwardsBodyAndJwtVerbatim()
    {
        const string body = """{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE","item_id":"plaid-item-1"}""";
        const string jwt = "header.payload.signature";
        string? forwardedJwt = null;
        string? forwardedBody = null;
        _manager.Setup(m => m.HandleTransactionsWebhookAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((j, b) => { forwardedJwt = j; forwardedBody = b; })
            .ReturnsAsync(Result.Success());
        var request = BuildRequest(body, jwt);

        var result = await PlaidEndpoints.HandleWebhookAsync(request, _manager.Object);

        AssertOk(result);
        Assert.Equal(jwt, forwardedJwt);
        Assert.Equal(body, forwardedBody); // verbatim — no re-serialization
        _manager.Verify(m => m.HandleTransactionsWebhookAsync(jwt, body), Times.Once);
    }

    [Fact]
    public async Task Webhook_MissingVerificationHeader_Returns200AndForwardsEmptyJwt()
    {
        const string body = """{"webhook_type":"TRANSACTIONS"}""";
        string? forwardedJwt = null;
        _manager.Setup(m => m.HandleTransactionsWebhookAsync(It.IsAny<string>(), body))
            .Callback<string, string>((j, _) => forwardedJwt = j)
            .ReturnsAsync(Result.Success());
        var request = BuildRequest(body); // no header

        var result = await PlaidEndpoints.HandleWebhookAsync(request, _manager.Object);

        // Neutral 200; the manager receives an empty JWT and (proven elsewhere) does no sync.
        AssertOk(result);
        Assert.Equal(string.Empty, forwardedJwt);
    }

    [Fact]
    public async Task Webhook_DuplicateVerificationHeader_Returns200AndForwardsCommaJoinedMalformedJwt()
    {
        const string body = """{"webhook_type":"TRANSACTIONS"}""";
        string? forwardedJwt = null;
        _manager.Setup(m => m.HandleTransactionsWebhookAsync(It.IsAny<string>(), body))
            .Callback<string, string>((j, _) => forwardedJwt = j)
            .ReturnsAsync(Result.Success());
        // Two header values get comma-joined into a malformed JWT string.
        var request = BuildRequest(body, "header.a.sig", "header.b.sig");

        var result = await PlaidEndpoints.HandleWebhookAsync(request, _manager.Object);

        AssertOk(result);
        Assert.NotNull(forwardedJwt);
        Assert.Contains(",", forwardedJwt!); // comma-joined → not a valid 3-segment JWT → manager rejects
    }

    [Fact]
    public async Task Webhook_EmptyBody_Returns200AndForwardsEmptyBody()
    {
        string? forwardedBody = null;
        _manager.Setup(m => m.HandleTransactionsWebhookAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, b) => forwardedBody = b)
            .ReturnsAsync(Result.Success());
        var request = BuildRequest(string.Empty, "header.payload.sig");

        var result = await PlaidEndpoints.HandleWebhookAsync(request, _manager.Object);

        AssertOk(result);
        Assert.Equal(string.Empty, forwardedBody);
    }
}
