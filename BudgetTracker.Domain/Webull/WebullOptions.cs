namespace BudgetTracker.Domain.Webull;

/// <summary>
/// Configuration bound from the "Webull" section. Injected via <see cref="Microsoft.Extensions.Options.IOptions{T}"/>.
/// <see cref="AppKey"/> and <see cref="AppSecret"/> are entered by hand: locally in the gitignored
/// <c>appsettings.Local.json</c>, on Lambda in SSM (<c>/budgettracker/Webull/AppKey</c>, <c>/budgettracker/Webull/AppSecret</c>).
/// No UI ever accepts them.
/// </summary>
public class WebullOptions
{
    public const string SectionName = "Webull";

    /// <summary>Sent as <c>x-app-key</c>. Never returned to the browser.</summary>
    public string AppKey { get; set; } = string.Empty;

    /// <summary>The HMAC signing key, and nothing else. Never sent, logged, or returned.</summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>
    /// Webull's HTTP API host. Sandbox unless config explicitly says production (<c>https://api.webull.com</c>).
    /// The signature's <c>host</c> value is this URL's host.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.sandbox.webull.com";

    /// <summary>True when both keys are present. When false, no card renders and no Webull call is made.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(AppKey) && !string.IsNullOrWhiteSpace(AppSecret);
}
