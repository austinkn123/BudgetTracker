using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Plaid;
using Microsoft.IdentityModel.Tokens;

namespace BudgetTracker.Tests.Engines;

/// <summary>
/// Unit tests for the pure Plaid webhook verification engine. A real ES256 keypair is generated
/// per test so a genuinely-valid token can be signed, then individual guarantees are broken to
/// prove each failure path returns false.
/// </summary>
public class PlaidWebhookEngineTests
{
    private const string Kid = "test-key-1";
    private readonly PlaidWebhookEngine _sut = new();

    // ── ExtractKeyId ────────────────────────────────────────────────────────

    [Fact]
    public void ExtractKeyId_ValidEs256Header_ReturnsKid()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (jwt, _) = SignedToken(key, "{}");

        var kid = _sut.ExtractKeyId(jwt);

        Assert.Equal(Kid, kid);
    }

    [Fact]
    public void ExtractKeyId_NonEs256Alg_ReturnsNull()
    {
        var header = Base64Url(Encoding.UTF8.GetBytes("""{"alg":"HS256","kid":"test-key-1"}"""));
        var payload = Base64Url(Encoding.UTF8.GetBytes("{}"));
        var jwt = $"{header}.{payload}.signature";

        Assert.Null(_sut.ExtractKeyId(jwt));
    }

    [Fact]
    public void ExtractKeyId_MalformedToken_ReturnsNull()
    {
        Assert.Null(_sut.ExtractKeyId("not-a-jwt"));
    }

    [Fact]
    public void ExtractKeyId_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(_sut.ExtractKeyId(null!));
        Assert.Null(_sut.ExtractKeyId(""));
    }

    // ── VerifyWebhook — happy path ──────────────────────────────────────────

    [Fact]
    public void VerifyWebhook_ValidToken_ReturnsTrue()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string body = """{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE"}""";
        var (jwt, jwk) = SignedToken(key, body);

        Assert.True(_sut.VerifyWebhook(jwt, jwk, body));
    }

    // ── VerifyWebhook — failure paths ───────────────────────────────────────

    [Fact]
    public void VerifyWebhook_TamperedSignature_ReturnsFalse()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string body = """{"webhook_type":"TRANSACTIONS"}""";
        var (jwt, jwk) = SignedToken(key, body);

        // Flip the last char of the signature segment.
        var lastChar = jwt[^1] == 'A' ? 'B' : 'A';
        var tampered = jwt[..^1] + lastChar;

        Assert.False(_sut.VerifyWebhook(tampered, jwk, body));
    }

    [Fact]
    public void VerifyWebhook_SignedByDifferentKey_ReturnsFalse()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string body = """{"webhook_type":"TRANSACTIONS"}""";
        var (jwt, _) = SignedToken(signingKey, body);
        var wrongJwk = Jwk(otherKey);

        Assert.False(_sut.VerifyWebhook(jwt, wrongJwk, body));
    }

    [Fact]
    public void VerifyWebhook_StaleIat_ReturnsFalse()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string body = """{"webhook_type":"TRANSACTIONS"}""";
        var staleIat = DateTimeOffset.UtcNow.AddMinutes(-6).ToUnixTimeSeconds();
        var (jwt, jwk) = SignedToken(key, body, iat: staleIat);

        Assert.False(_sut.VerifyWebhook(jwt, jwk, body));
    }

    [Fact]
    public void VerifyWebhook_BodyHashMismatch_ReturnsFalse()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string signedBody = """{"webhook_type":"TRANSACTIONS"}""";
        var (jwt, jwk) = SignedToken(key, signedBody);

        // The engine hashes THIS body, which differs from the one embedded in the JWT claim.
        Assert.False(_sut.VerifyWebhook(jwt, jwk, """{"webhook_type":"ITEM"}"""));
    }

    // ── VerifyWebhook — iat freshness boundaries ────────────────────────────

    [Fact]
    public void VerifyWebhook_IatExactlyAtFiveMinuteBoundary_Accepts()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string body = """{"webhook_type":"TRANSACTIONS"}""";
        // Exactly 5:00 old is inclusive-accept (age > 5min rejects; == 5min passes).
        var iat = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var (jwt, jwk) = SignedToken(key, body, iat: iat);

        Assert.True(_sut.VerifyWebhook(jwt, jwk, body));
    }

    [Fact]
    public void VerifyWebhook_IatJustOverFiveMinutes_Rejects()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string body = """{"webhook_type":"TRANSACTIONS"}""";
        var iat = DateTimeOffset.UtcNow.AddMinutes(-5).AddSeconds(-30).ToUnixTimeSeconds();
        var (jwt, jwk) = SignedToken(key, body, iat: iat);

        Assert.False(_sut.VerifyWebhook(jwt, jwk, body));
    }

    [Fact]
    public void VerifyWebhook_FutureIatBeyondSkew_Rejects()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string body = """{"webhook_type":"TRANSACTIONS"}""";
        // 5 minutes in the future — well beyond the ~60s clock-skew allowance. Pins the future-iat fix.
        var iat = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
        var (jwt, jwk) = SignedToken(key, body, iat: iat);

        Assert.False(_sut.VerifyWebhook(jwt, jwk, body));
    }

    [Fact]
    public void VerifyWebhook_FutureIatWithinSkew_Accepts()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string body = """{"webhook_type":"TRANSACTIONS"}""";
        // 10s in the future — inside the ~60s skew allowance, still accepted.
        var iat = DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeSeconds();
        var (jwt, jwk) = SignedToken(key, body, iat: iat);

        Assert.True(_sut.VerifyWebhook(jwt, jwk, body));
    }

    [Fact]
    public void VerifyWebhook_MissingIat_Rejects()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string body = """{"webhook_type":"TRANSACTIONS"}""";
        var (jwt, jwk) = SignedRawPayload(key, $$"""{"request_body_sha256":"{{BodyHashHex(body)}}"}""");

        Assert.False(_sut.VerifyWebhook(jwt, jwk, body));
    }

    [Fact]
    public void VerifyWebhook_NonNumericIat_Rejects()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        const string body = """{"webhook_type":"TRANSACTIONS"}""";
        var (jwt, jwk) = SignedRawPayload(key, $$"""{"iat":"not-a-number","request_body_sha256":"{{BodyHashHex(body)}}"}""");

        Assert.False(_sut.VerifyWebhook(jwt, jwk, body));
    }

    // ── VerifyWebhook — request_body_sha256 claim presence ──────────────────

    [Fact]
    public void VerifyWebhook_MissingBodyHashClaim_Rejects()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var (jwt, jwk) = SignedRawPayload(key, $$"""{"iat":{{iat}}}""");

        Assert.False(_sut.VerifyWebhook(jwt, jwk, "{}"));
    }

    [Fact]
    public void VerifyWebhook_EmptyBodyHashClaim_Rejects()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var (jwt, jwk) = SignedRawPayload(key, $$"""{"iat":{{iat}},"request_body_sha256":""}""");

        Assert.False(_sut.VerifyWebhook(jwt, jwk, "{}"));
    }

    [Fact]
    public void VerifyWebhook_MalformedToken_ReturnsFalse()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var jwk = Jwk(key);

        Assert.False(_sut.VerifyWebhook("garbage", jwk, "{}"));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a Plaid-style ES256 JWT signed with <paramref name="key"/> whose payload carries a fresh
    /// (or supplied) <c>iat</c> and a <c>request_body_sha256</c> claim matching <paramref name="body"/>.
    /// Returns the compact JWT plus the matching public JWK.
    /// </summary>
    private static (string Jwt, PlaidJwkDto Jwk) SignedToken(ECDsa key, string body, long? iat = null)
    {
        var issuedAt = iat ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payloadJson = $$"""{"iat":{{issuedAt}},"request_body_sha256":"{{BodyHashHex(body)}}"}""";
        return SignedRawPayload(key, payloadJson);
    }

    /// <summary>Signs an ES256 JWT over an arbitrary raw payload JSON (used to omit/malform specific claims).</summary>
    private static (string Jwt, PlaidJwkDto Jwk) SignedRawPayload(ECDsa key, string payloadJson)
    {
        var headerJson = $$"""{"alg":"ES256","kid":"{{Kid}}","typ":"JWT"}""";
        var signingInput = $"{Base64Url(Encoding.UTF8.GetBytes(headerJson))}.{Base64Url(Encoding.UTF8.GetBytes(payloadJson))}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256);
        var jwt = $"{signingInput}.{Base64Url(signature)}";

        return (jwt, Jwk(key));
    }

    private static string BodyHashHex(string body) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    /// <summary>Projects the public parameters of an ES256 key into a <see cref="PlaidJwkDto"/>.</summary>
    private static PlaidJwkDto Jwk(ECDsa key)
    {
        var p = key.ExportParameters(includePrivateParameters: false);
        return new PlaidJwkDto(
            Kty: "EC",
            Crv: "P-256",
            X: Base64Url(p.Q.X!),
            Y: Base64Url(p.Q.Y!),
            Kid: Kid,
            Use: "sig",
            Alg: "ES256");
    }

    private static string Base64Url(byte[] bytes) => Base64UrlEncoder.Encode(bytes);
}
