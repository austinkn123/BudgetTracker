namespace BudgetTracker.Domain.Webull;

/// <summary>Access-token status as Webull's Create Token and Check Token APIs report it.</summary>
public enum WebullTokenStatus
{
    /// <summary>Created; waiting for the user to approve it in the Webull app with the SMS code (5-minute window).</summary>
    Pending,

    /// <summary>Approved and usable.</summary>
    Normal,

    /// <summary>Not approved within the 5-minute window.</summary>
    Expired,

    /// <summary>Unusable: 15 consecutive days without API calls, or unknown to Webull.</summary>
    Invalid
}

/// <summary>Why a Webull call failed. Carried by <see cref="WebullApiException"/> and stored as the last error.</summary>
public enum WebullErrorKind
{
    /// <summary>Network failure, timeout, 429, 5xx, or an unreadable response. Transient: token status is left alone.</summary>
    Unreachable,

    /// <summary>Webull rejected the App Key or the signature. The user must fix config.</summary>
    KeyRejected,

    /// <summary>Webull rejected the access token. The user must verify again.</summary>
    TokenRejected
}

/// <summary>The single state the Settings card shows.</summary>
public enum WebullState
{
    /// <summary><see cref="WebullOptions.IsConfigured"/> is false.</summary>
    NotSetUp,

    /// <summary>Configured, but no token has been created.</summary>
    NotConnected,

    /// <summary>Token pending and still inside its 5-minute approval window.</summary>
    WaitingForApproval,

    /// <summary>Token approved and not rejected since.</summary>
    Connected,

    /// <summary>Token still pending past its window, or Webull reported it expired.</summary>
    ApprovalExpired,

    /// <summary>Token invalid, rejected by Webull, or unreadable (Data Protection key ring lost).</summary>
    NeedsVerification
}

/// <summary>What a refresh should do next. Creating a token is never one of the options.</summary>
public enum WebullRefreshAction
{
    /// <summary>Make no Webull call.</summary>
    None,

    /// <summary>Ask Webull whether the pending token has been approved.</summary>
    CheckToken,

    /// <summary>Fetch every account's balance (from Phase 2, with its positions) with the approved token.</summary>
    FetchBalances
}

/// <summary>
/// A freshly created access token, in plaintext. Persist it only through the connection accessor (which encrypts it).
/// Never log it or return it to the client.
/// </summary>
/// <param name="AccessToken">The plaintext token.</param>
/// <param name="Status">Its status at creation: Pending in production, Normal in sandbox.</param>
public sealed record WebullToken(string AccessToken, WebullTokenStatus Status)
{
    /// <summary>Redacted, so logging the record never leaks the token.</summary>
    public override string ToString() => $"WebullToken {{ Status = {Status} }}";
}

/// <summary>One Webull account's balance as of a fetch. Also the shape the API returns per account.</summary>
/// <param name="AccountId">Webull's <c>account_id</c>. Opaque, not a credential.</param>
/// <param name="AccountLabel">Webull's <c>account_label</c> (e.g. "Individual Cash"), or null when absent.</param>
/// <param name="AccountType">Webull's <c>account_type</c> (e.g. <c>CASH</c>, <c>MARGIN</c>).</param>
/// <param name="TotalNetLiquidationValue">Webull's <c>total_net_liquidation_value</c>: the account's total value.</param>
/// <param name="TotalCashBalance">Webull's <c>total_cash_balance</c>, or null when Webull sends it empty.</param>
/// <param name="Currency">Webull's <c>total_asset_currency</c> (e.g. <c>USD</c>).</param>
public record WebullBalance(
    string AccountId,
    string? AccountLabel,
    string AccountType,
    decimal TotalNetLiquidationValue,
    decimal? TotalCashBalance,
    string Currency);

/// <summary>What is stored about the user's Webull link. Never carries the access token.</summary>
/// <param name="TokenStatus">Status of the stored token, or null when none has been created.</param>
/// <param name="TokenCreatedAt">When the stored token was created (UTC), or null when none has been created.</param>
/// <param name="LastFetchedAt">When <paramref name="Balances"/> were fetched (UTC), or null when never fetched.</param>
/// <param name="LastError">Why the most recent Webull call failed, or null when it succeeded.</param>
/// <param name="Balances">The last successful fetch's balances; empty when never fetched.</param>
public record WebullSnapshot(
    WebullTokenStatus? TokenStatus,
    DateTime? TokenCreatedAt,
    DateTime? LastFetchedAt,
    WebullErrorKind? LastError,
    IReadOnlyList<WebullBalance> Balances);

/// <summary>
/// The Webull view both the Dashboard and Settings cards render. Safe to return over the API: no App Key, no secret, no token.
/// </summary>
/// <param name="State">The single state the Settings card shows.</param>
/// <param name="Balances">One row per account from the last successful fetch; empty when none, or when <see cref="WebullState.NotSetUp"/>.</param>
/// <param name="AsOf">When <paramref name="Balances"/> were fetched (UTC), or null when never fetched.</param>
/// <param name="IsStale">True when <paramref name="AsOf"/> is more than 24 hours old.</param>
/// <param name="LastError">Why the most recent Webull call failed, or null when it succeeded.</param>
/// <param name="ApprovalExpiresAt">Token creation time + 5 minutes (UTC); set only in <see cref="WebullState.WaitingForApproval"/>.</param>
/// <param name="CanVerify">True only when "Verify in Webull app" may be offered: NotConnected, ApprovalExpired, NeedsVerification.</param>
public record WebullConnectionView(
    WebullState State,
    IReadOnlyList<WebullBalance> Balances,
    DateTime? AsOf,
    bool IsStale,
    WebullErrorKind? LastError,
    DateTime? ApprovalExpiresAt,
    bool CanVerify);
