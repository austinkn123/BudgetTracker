using System.Net;
using System.Text;
using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Plaid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Tests.Accessors;

/// <summary>
/// Tests for <see cref="PlaidAccessor"/>'s webhook-key caching. Uses a counting fake
/// <see cref="HttpMessageHandler"/> as the testable seam (no accessor tests existed before, and this
/// avoids standing up any server/HTTP infra). Proves the JWK cache removes the per-request Plaid
/// round-trip that produced the webhook-verification timing oracle.
/// </summary>
public class PlaidAccessorTests
{
    /// <summary>Fake handler that returns a canned JWK response and counts how many requests it sends.</summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            const string json = """
                {"key":{"kty":"EC","crv":"P-256","x":"abc","y":"def","kid":"kid-1","use":"sig","alg":"ES256"},"request_id":"r1"}
                """;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private static PlaidAccessor BuildAccessor(CountingHandler handler, IMemoryCache cache)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.plaid.com") };
        var options = Options.Create(new PlaidOptions { ClientId = "cid", Secret = "secret" });
        return new PlaidAccessor(http, options, cache);
    }

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
