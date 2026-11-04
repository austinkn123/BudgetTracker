using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>
/// Signed, read-only HTTP calls to Webull's OpenAPI. Signs every request with HMAC keyed by
/// <see cref="WebullOptions.AppSecret"/>, which is never sent. Holds a path allowlist (POST <c>/auth/tokens/create</c>,
/// POST <c>/auth/tokens/check</c>, GET <c>/trading/accounts/list</c>, GET <c>/trading/assets/balances/get</c>, GET
/// <c>/trading/assets/positions/list</c>, plus GET <c>/trading/orders/historical-orders/list</c> for
/// <see cref="IWebullOrderHistoryAccessor"/>) and throws before sending any other request. Refuses to send anything when
/// <see cref="WebullOptions.IsConfigured"/> is false.
/// Every failure surfaces as <see cref="WebullApiException"/>. Never persists, never decides business outcomes.
/// </summary>
public interface IWebullAccessor
{
    /// <summary>
    /// Create a new access token (<c>POST /auth/tokens/create</c>). In production this texts an SMS code to the user, so the
    /// only permitted caller is the user's explicit "Verify in Webull app" action. Production tokens come back
    /// <see cref="WebullTokenStatus.Pending"/>; sandbox tokens come back <see cref="WebullTokenStatus.Normal"/>.
    /// </summary>
    Task<WebullToken> CreateTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>The current status of <paramref name="accessToken"/> (<c>POST /auth/tokens/check</c>). Never creates a token.</summary>
    Task<WebullTokenStatus> CheckTokenAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every account's balance and positions in one paced pass: <c>GET /trading/accounts/list</c>, then for each account
    /// <c>GET /trading/assets/balances/get</c> and, unless that balance was skipped, <c>GET /trading/assets/positions/list</c>
    /// (the fifth allowlist entry). Calls are at least 1.1 s apart. That makes 1 + 2N calls for N accounts.
    /// All or nothing: any failed call throws <see cref="WebullApiException"/> and nothing is returned. An account whose
    /// balance has no <c>total_net_liquidation_value</c> (Webull sends it empty for EVENTS_CASH) is left out, never returned
    /// as $0, and gets no positions call. A numeric field Webull sends empty or omits becomes null. A field that is present
    /// but not a number is an unreadable response. Option legs, strategy and event outcome are not read.
    /// </summary>
    Task<IReadOnlyList<WebullAccountPortfolio>> GetPortfolioAsync(string accessToken, CancellationToken cancellationToken = default);
}
