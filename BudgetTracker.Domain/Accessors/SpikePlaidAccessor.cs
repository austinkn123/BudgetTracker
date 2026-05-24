// [SPIKE] BUD-4 — throwaway. Do not merge to main without replacing SpikePlaidAccessor.

using System.Net.Http.Json;
using System.Text.Json;
using BudgetTracker.Domain.Interfaces.Accessors;

namespace BudgetTracker.Domain.Accessors;

/// <summary>
/// Spike-only Plaid accessor that communicates with the Plaid Sandbox API via raw HttpClient.
/// Replace with a production-grade accessor (SDK or typed client) before merging to main.
/// </summary>
public class SpikePlaidAccessor(
    IHttpClientFactory httpClientFactory,
    string clientId,
    string secret) : IPlaidAccessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<string> CreateLinkTokenAsync(int userId)
    {
        var client = httpClientFactory.CreateClient("Plaid");

        var body = new
        {
            client_id = clientId,
            secret,
            client_name = "BudgetTracker",
            country_codes = new[] { "US" },
            language = "en",
            user = new { client_user_id = userId.ToString() },
            products = new[] { "transactions" }
        };

        var response = await client.PostAsJsonAsync("/link/token/create", body, JsonOptions);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return doc.RootElement.GetProperty("link_token").GetString()
               ?? throw new InvalidOperationException("Plaid response missing link_token");
    }

    /// <inheritdoc />
    public async Task<string> ExchangePublicTokenAsync(string publicToken)
    {
        var client = httpClientFactory.CreateClient("Plaid");

        var body = new
        {
            client_id = clientId,
            secret,
            public_token = publicToken
        };

        var response = await client.PostAsJsonAsync("/item/public_token/exchange", body, JsonOptions);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return doc.RootElement.GetProperty("access_token").GetString()
               ?? throw new InvalidOperationException("Plaid response missing access_token");
    }
}
