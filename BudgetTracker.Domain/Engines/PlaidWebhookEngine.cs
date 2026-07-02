using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Plaid;
using Microsoft.IdentityModel.Tokens;

namespace BudgetTracker.Domain.Engines;

/// <summary>
/// Pure ES256 verification for Plaid's <c>Plaid-Verification</c> webhook JWT. Stateless, no I/O:
/// the Manager fetches the JWK and passes it in. Every failure path — bad signature, stale token,
/// body-hash mismatch, or any thrown exception — collapses to <c>false</c>/<c>null</c> so the
/// caller can reject silently and leak nothing.
/// </summary>
public class PlaidWebhookEngine : IPlaidWebhookEngine
{
    private const string ExpectedAlgorithm = "ES256";

    /// <summary>Plaid webhooks older than this are treated as replays and rejected.</summary>
    private static readonly TimeSpan MaxTokenAge = TimeSpan.FromMinutes(5);

    /// <summary>Clock-skew allowance applied symmetrically — tolerates minor drift between Plaid and this host.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    public string? ExtractKeyId(string plaidVerificationJwt)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(plaidVerificationJwt))
                return null;

            var segments = plaidVerificationJwt.Split('.');
            if (segments.Length != 3)
                return null;

            using var header = JsonDocument.Parse(Base64UrlEncoder.Decode(segments[0]));
            var root = header.RootElement;

            // Only ES256 is trusted — reject anything else (defends against alg-substitution).
            if (!root.TryGetProperty("alg", out var algProp) || algProp.GetString() != ExpectedAlgorithm)
                return null;

            return root.TryGetProperty("kid", out var kidProp) ? kidProp.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool VerifyWebhook(string plaidVerificationJwt, PlaidJwkDto jwk, string rawBody)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(plaidVerificationJwt))
                return false;

            var segments = plaidVerificationJwt.Split('.');
            if (segments.Length != 3)
                return false;

            if (!VerifySignature(segments, jwk))
                return false;

            using var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(segments[1]));
            var claims = payload.RootElement;

            if (IatOutsideValidWindow(claims))
                return false;

            return BodyHashMatches(claims, rawBody);
        }
        catch
        {
            // Any parse/crypto failure is a rejection, never a throw the caller must handle.
            return false;
        }
    }

    /// <summary>Verifies the ES256 signature of the signing input (<c>header.payload</c>) against the JWK.</summary>
    private static bool VerifySignature(string[] segments, PlaidJwkDto jwk)
    {
        if (jwk.Alg is not null && jwk.Alg != ExpectedAlgorithm)
            return false;
        if (string.IsNullOrEmpty(jwk.X) || string.IsNullOrEmpty(jwk.Y))
            return false;

        var parameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = Base64UrlEncoder.DecodeBytes(jwk.X),
                Y = Base64UrlEncoder.DecodeBytes(jwk.Y)
            }
        };

        using var ecdsa = ECDsa.Create(parameters);
        var signingInput = Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}");
        var signature = Base64UrlEncoder.DecodeBytes(segments[2]);

        // JWS ES256 uses the raw fixed-width (R||S) format, not DER.
        return ecdsa.VerifyData(signingInput, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <summary>
    /// Rejects tokens whose <c>iat</c> is missing, non-numeric, older than <see cref="MaxTokenAge"/>, or
    /// dated more than <see cref="ClockSkew"/> in the future. A future <c>iat</c> would otherwise yield a
    /// negative age and be accepted indefinitely, defeating the replay window. Compared at whole-second
    /// granularity so the 5:00 boundary is a deterministic inclusive-accept.
    /// </summary>
    private static bool IatOutsideValidWindow(JsonElement claims)
    {
        if (!claims.TryGetProperty("iat", out var iatProp) || iatProp.ValueKind != JsonValueKind.Number
            || !iatProp.TryGetInt64(out var iat))
            return true;

        var ageSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - iat;

        // Too old (replay) or too far in the future (clock spoofing / bad clock).
        return ageSeconds > MaxTokenAge.TotalSeconds || ageSeconds < -ClockSkew.TotalSeconds;
    }

    /// <summary>Constant-time compare of SHA-256(rawBody) against the token's <c>request_body_sha256</c> claim.</summary>
    private static bool BodyHashMatches(JsonElement claims, string rawBody)
    {
        if (!claims.TryGetProperty("request_body_sha256", out var hashProp))
            return false;

        var claimedHash = hashProp.GetString();
        if (string.IsNullOrEmpty(claimedHash))
            return false;

        var computedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computedHash),
            Encoding.ASCII.GetBytes(claimedHash));
    }
}
