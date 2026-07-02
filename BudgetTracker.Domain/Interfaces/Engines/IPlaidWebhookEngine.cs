using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>
/// Pure cryptographic verification for inbound Plaid webhooks. No I/O, no data access —
/// the Manager fetches the JWK and feeds it in. All failures (including caught exceptions)
/// surface as <c>false</c>/<c>null</c> so the caller can reject silently.
/// </summary>
public interface IPlaidWebhookEngine
{
    /// <summary>
    /// Decode the <c>Plaid-Verification</c> JWT header (base64url) and return its <c>kid</c> claim,
    /// used to look up the verification key. Returns null if the header is malformed or its
    /// <c>alg</c> is not <c>ES256</c>.
    /// </summary>
    /// <param name="plaidVerificationJwt">The raw JWT from the <c>Plaid-Verification</c> header.</param>
    string? ExtractKeyId(string plaidVerificationJwt);

    /// <summary>
    /// Verify a webhook is authentic: ES256 signature valid against <paramref name="jwk"/>, the
    /// <c>iat</c> claim no older than 5 minutes (replay protection), and the JWT's
    /// <c>request_body_sha256</c> claim equal (constant-time) to SHA-256 of <paramref name="rawBody"/>.
    /// Returns false on any failure.
    /// </summary>
    /// <param name="plaidVerificationJwt">The raw JWT from the <c>Plaid-Verification</c> header.</param>
    /// <param name="jwk">The JSON Web Key fetched for the JWT's <c>kid</c>.</param>
    /// <param name="rawBody">The verbatim request body bytes (as a UTF-8 string) to hash and compare.</param>
    bool VerifyWebhook(string plaidVerificationJwt, PlaidJwkDto jwk, string rawBody);
}
