// [SPIKE] BUD-4 — throwaway. Do not merge to main without replacing SpikePlaidAccessor.

namespace BudgetTracker.Server.Options;

/// <summary>
/// Strongly-typed configuration for Plaid API credentials and environment.
/// Populated from the "Plaid" section of user-secrets / appsettings.
/// </summary>
public record PlaidOptions
{
    /// <summary>Plaid client_id from the Plaid dashboard.</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>Plaid secret for the target environment.</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>Target Plaid environment (sandbox | development | production).</summary>
    public string Environment { get; init; } = "sandbox";

    /// <summary>Base URL for the Plaid API (e.g. https://sandbox.plaid.com).</summary>
    public string BaseUrl { get; init; } = "https://sandbox.plaid.com";
}
