using System.Net;
using System.Text;
using System.Text.Json;
using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Plaid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Tests.Accessors;

/// <summary>
/// Tests for <see cref="PlaidAccessor"/>: request-body shape, typed Plaid errors, and webhook-key caching. Uses a
/// counting fake <see cref="HttpMessageHandler"/> as the testable seam (avoids standing up any server/HTTP infra).
/// The caching tests prove the JWK cache removes the per-request Plaid round-trip that produced the
/// webhook-verification timing oracle.
/// </summary>
public class PlaidAccessorTests
{
    private const string JwkJson = """
        {"key":{"kty":"EC","crv":"P-256","x":"abc","y":"def","kid":"kid-1","use":"sig","alg":"ES256"},"request_id":"r1"}
        """;

    private const string LinkTokenJson = """
        {"link_token":"link-sandbox-abc","expiration":"2026-10-10T12:00:00Z","request_id":"r1"}
        """;

    /// <summary>Fake handler that returns a canned response, counts sends, and records the last request body.</summary>
    private sealed class CountingHandler(HttpStatusCode status = HttpStatusCode.OK, string json = JwkJson) : HttpMessageHandler
    {
        public int SendCount { get; private set; }
        public JsonElement LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            LastBody = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private static PlaidAccessor BuildAccessor(CountingHandler handler, IMemoryCache cache, string redirectUri = "")
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.plaid.com") };
        var options = Options.Create(new PlaidOptions { ClientId = "cid", Secret = "secret", RedirectUri = redirectUri });
        return new PlaidAccessor(http, options, cache);
    }

    // ── CreateLinkTokenAsync ────────────────────────────────────────────────

    [Fact]
    public async Task CreateLinkToken_NewLink_SendsProductsAndNoAccessToken()
    {
        var handler = new CountingHandler(json: LinkTokenJson);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var token = await BuildAccessor(handler, cache).CreateLinkTokenAsync("sub-1");

        Assert.Equal("link-sandbox-abc", token.LinkToken);
        Assert.True(handler.LastBody.TryGetProperty("products", out _));
        Assert.False(handler.LastBody.TryGetProperty("access_token", out _));
    }

    [Fact]
    public async Task CreateLinkToken_UpdateMode_SendsAccessTokenAndOmitsProducts()
    {
        // Plaid update mode: access_token identifies the item to repair, and products must be absent.
        var handler = new CountingHandler(json: LinkTokenJson);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await BuildAccessor(handler, cache).CreateLinkTokenAsync("sub-1", accessToken: "access-token-xyz");

        Assert.Equal("access-token-xyz", handler.LastBody.GetProperty("access_token").GetString());
        Assert.False(handler.LastBody.TryGetProperty("products", out _));
        Assert.Equal("sub-1", handler.LastBody.GetProperty("user").GetProperty("client_user_id").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("access-token-xyz")]
    public async Task CreateLinkToken_RedirectUriConfigured_SendsRedirectUriInBothModes(string? accessToken)
    {
        var handler = new CountingHandler(json: LinkTokenJson);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var accessor = BuildAccessor(handler, cache, redirectUri: "https://app.example/plaid-oauth");

        await accessor.CreateLinkTokenAsync("sub-1", accessToken);

        Assert.Equal("https://app.example/plaid-oauth", handler.LastBody.GetProperty("redirect_uri").GetString());
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("", "access-token-xyz")]
    [InlineData("   ", "access-token-xyz")]
    public async Task CreateLinkToken_RedirectUriNotConfigured_OmitsRedirectUriInBothModes(string redirectUri, string? accessToken)
    {
        // Plaid rejects a redirect_uri that is not allow-listed, so an unset one must not be sent at all.
        var handler = new CountingHandler(json: LinkTokenJson);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await BuildAccessor(handler, cache, redirectUri).CreateLinkTokenAsync("sub-1", accessToken);

        Assert.False(handler.LastBody.TryGetProperty("redirect_uri", out _));
    }

    // ── Error handling ──────────────────────────────────────────────────────

    [Fact]
    public async Task NonSuccessResponse_ThrowsPlaidApiExceptionWithErrorCodeAndSanitizedMessage()
    {
        var handler = new CountingHandler(HttpStatusCode.BadRequest, """
            {"error_type":"ITEM_ERROR","error_code":"ITEM_LOGIN_REQUIRED","error_message":"the login details of this item have changed"}
            """);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var ex = await Assert.ThrowsAsync<PlaidApiException>(
            () => BuildAccessor(handler, cache).SyncTransactionsAsync("access-token-xyz", cursor: null));

        Assert.Equal("ITEM_LOGIN_REQUIRED", ex.ErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.DoesNotContain("secret", ex.Message);
        Assert.DoesNotContain("access-token-xyz", ex.Message);
    }

    [Fact]
    public async Task NonSuccessResponse_UnparseableBody_ThrowsPlaidApiExceptionWithNullErrorCode()
    {
        var handler = new CountingHandler(HttpStatusCode.InternalServerError, "<html>bad gateway</html>");
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var ex = await Assert.ThrowsAsync<PlaidApiException>(() => BuildAccessor(handler, cache).CreateLinkTokenAsync("sub-1"));

        Assert.Null(ex.ErrorCode);
    }

    [Fact]
    public async Task ResetSandboxLogin_PostsAccessToken()
    {
        var handler = new CountingHandler(json: """{"reset_login":true,"request_id":"r1"}""");
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await BuildAccessor(handler, cache).ResetSandboxLoginAsync("access-token-xyz");

        Assert.Equal(1, handler.SendCount);
        Assert.Equal("access-token-xyz", handler.LastBody.GetProperty("access_token").GetString());
    }

    // ── GetWebhookVerificationKeyAsync ──────────────────────────────────────

    [Fact]
    public async Task GetWebhookVerificationKey_SecondCallSameKid_DoesNotReHitNetwork()
    {
        var handler = new CountingHandler();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var accessor = BuildAccessor(handler, cache);

        var first = await accessor.GetWebhookVerificationKeyAsync("kid-1");
        var second = await accessor.GetWebhookVerificationKeyAsync("kid-1");

        // Exactly one network send for two lookups of the same kid — the second is served from cache.
        Assert.Equal(1, handler.SendCount);
        Assert.Equal("kid-1", first.Kid);
        Assert.Equal(first, second); // same cached DTO
    }

    [Fact]
    public async Task GetWebhookVerificationKey_DifferentKid_FetchesAgain()
    {
        var handler = new CountingHandler();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var accessor = BuildAccessor(handler, cache);

        await accessor.GetWebhookVerificationKeyAsync("kid-1");
        await accessor.GetWebhookVerificationKeyAsync("kid-2");

        // Distinct kids are cached independently, so each triggers its own fetch.
        Assert.Equal(2, handler.SendCount);
    }

    [Fact]
    public async Task GetWebhookVerificationKey_FirstCall_ReturnsMappedJwk()
    {
        var handler = new CountingHandler();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var accessor = BuildAccessor(handler, cache);

        var jwk = await accessor.GetWebhookVerificationKeyAsync("kid-1");

        Assert.Equal("EC", jwk.Kty);
        Assert.Equal("P-256", jwk.Crv);
        Assert.Equal("abc", jwk.X);
        Assert.Equal("def", jwk.Y);
        Assert.Equal("ES256", jwk.Alg);
    }
}
