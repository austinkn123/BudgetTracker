namespace BudgetTracker.Domain.Plaid;

/// <summary>
/// Configuration bound from the "Plaid" section of appsettings. Injected via <see cref="Microsoft.Extensions.Options.IOptions{T}"/>.
/// </summary>
public class PlaidOptions
{
    public const string SectionName = "Plaid";

    public string ClientId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string Environment { get; set; } = "sandbox";
    public string BaseUrl { get; set; } = "https://sandbox.plaid.com";

    /// <summary>
    /// Public HTTPS URL Plaid posts transaction webhooks to (e.g. an ngrok tunnel + <c>/api/plaid/webhook</c>).
    /// Empty means "no webhook" — sent on link create / item update only when non-empty so sandbox
    /// without a tunnel still works.
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>Interval (hours) for the backup sweep that re-syncs all active items. Default 6.</summary>
    public int SweepIntervalHours { get; set; } = 6;
}
