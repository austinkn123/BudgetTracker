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
}
