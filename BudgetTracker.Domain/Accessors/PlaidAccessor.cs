using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Plaid;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Domain.Accessors;

/// <summary>
/// Typed-HttpClient accessor for the Plaid REST API.
/// Sends client_id/secret on every request and returns plain DTOs. No business logic, no persistence.
/// </summary>
public class PlaidAccessor : IPlaidAccessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly PlaidOptions _options;

    /// <summary>
    /// Initializes the accessor. <paramref name="http"/> is configured by DI with the Plaid BaseUrl.
    /// </summary>
    public PlaidAccessor(HttpClient http, IOptions<PlaidOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<PlaidLinkTokenResult> CreateLinkTokenAsync(string clientUserId, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            client_id = _options.ClientId,
            secret = _options.Secret,
            client_name = "BudgetTracker",
            country_codes = new[] { "US" },
            language = "en",
            user = new { client_user_id = clientUserId },
            products = new[] { "transactions" }
        };

        var response = await PostAsync("/link/token/create", body, cancellationToken);
        var linkToken = response.GetProperty("link_token").GetString()
            ?? throw new InvalidOperationException("Plaid response missing link_token");
        var expiration = response.GetProperty("expiration").GetDateTime();
        return new PlaidLinkTokenResult(linkToken, expiration);
    }

    /// <inheritdoc />
    public async Task<PlaidExchangeResult> ExchangePublicTokenAsync(string publicToken, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            client_id = _options.ClientId,
            secret = _options.Secret,
            public_token = publicToken
        };

        var response = await PostAsync("/item/public_token/exchange", body, cancellationToken);
        var accessToken = response.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Plaid response missing access_token");
        var itemId = response.GetProperty("item_id").GetString()
            ?? throw new InvalidOperationException("Plaid response missing item_id");
        return new PlaidExchangeResult(accessToken, itemId);
    }

    /// <inheritdoc />
    public async Task<PlaidItemMetadata> GetItemMetadataAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            client_id = _options.ClientId,
            secret = _options.Secret,
            access_token = accessToken
        };

        var itemResponse = await PostAsync("/item/get", body, cancellationToken);
        var item = itemResponse.GetProperty("item");
        var itemId = item.GetProperty("item_id").GetString() ?? string.Empty;
        var institutionId = item.TryGetProperty("institution_id", out var instProp) && instProp.ValueKind != JsonValueKind.Null
            ? instProp.GetString() ?? string.Empty
            : string.Empty;

        DateTime? consentExpiresAt = null;
        if (item.TryGetProperty("consent_expiration_time", out var consentProp) && consentProp.ValueKind != JsonValueKind.Null)
        {
            if (consentProp.TryGetDateTime(out var consent))
                consentExpiresAt = consent;
        }

        var institutionName = "Unknown Institution";
        if (!string.IsNullOrEmpty(institutionId))
        {
            var instBody = new
            {
                client_id = _options.ClientId,
                secret = _options.Secret,
                institution_id = institutionId,
                country_codes = new[] { "US" }
            };
            var instResponse = await PostAsync("/institutions/get_by_id", instBody, cancellationToken);
            institutionName = instResponse.GetProperty("institution").GetProperty("name").GetString() ?? "Unknown Institution";
        }

        return new PlaidItemMetadata(itemId, institutionId, institutionName, consentExpiresAt);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlaidAccountDto>> GetAccountsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            client_id = _options.ClientId,
            secret = _options.Secret,
            access_token = accessToken
        };

        var response = await PostAsync("/accounts/get", body, cancellationToken);
        var accounts = new List<PlaidAccountDto>();
        foreach (var element in response.GetProperty("accounts").EnumerateArray())
        {
            accounts.Add(ReadAccount(element));
        }
        return accounts;
    }

    /// <inheritdoc />
    public async Task<PlaidSyncResult> SyncTransactionsAsync(string accessToken, string? cursor, CancellationToken cancellationToken = default)
    {
        var added = new List<PlaidTransactionDto>();
        var modified = new List<PlaidTransactionDto>();
        var removed = new List<string>();
        var nextCursor = cursor;
        bool hasMore;

        do
        {
            var body = new
            {
                client_id = _options.ClientId,
                secret = _options.Secret,
                access_token = accessToken,
                cursor = nextCursor,
                count = 500
            };

            var response = await PostAsync("/transactions/sync", body, cancellationToken);

            foreach (var element in response.GetProperty("added").EnumerateArray())
                added.Add(ReadTransaction(element));
            foreach (var element in response.GetProperty("modified").EnumerateArray())
                modified.Add(ReadTransaction(element));
            foreach (var element in response.GetProperty("removed").EnumerateArray())
                removed.Add(element.GetProperty("transaction_id").GetString() ?? string.Empty);

            nextCursor = response.GetProperty("next_cursor").GetString() ?? nextCursor;
            hasMore = response.GetProperty("has_more").GetBoolean();
        } while (hasMore);

        return new PlaidSyncResult(added, modified, removed, nextCursor ?? string.Empty);
    }

    /// <inheritdoc />
    public async Task RemoveItemAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            client_id = _options.ClientId,
            secret = _options.Secret,
            access_token = accessToken
        };

        try
        {
            _ = await PostAsync("/item/remove", body, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Idempotent: Plaid may return 400/ITEM_NOT_FOUND if already revoked. We ignore.
        }
    }

    private async Task<JsonElement> PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(path, body, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Plaid error body contains error_code/error_message. Surface a sanitized message —
            // never include the request body (which holds client_id/secret/access_token).
            string? errorCode = null;
            string? errorMessage = null;
            try
            {
                using var errDoc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                if (errDoc.RootElement.TryGetProperty("error_code", out var codeProp))
                    errorCode = codeProp.GetString();
                if (errDoc.RootElement.TryGetProperty("error_message", out var msgProp))
                    errorMessage = msgProp.GetString();
            }
            catch
            {
                // ignore — fall through with generic message
            }

            throw new HttpRequestException(
                $"Plaid API error (status {(int)response.StatusCode}): {errorCode ?? "unknown"} — {errorMessage ?? "no details"}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return doc.RootElement.Clone();
    }

    private static PlaidAccountDto ReadAccount(JsonElement element)
    {
        var accountId = element.GetProperty("account_id").GetString() ?? string.Empty;
        var name = element.GetProperty("name").GetString() ?? string.Empty;
        var mask = element.TryGetProperty("mask", out var maskProp) && maskProp.ValueKind == JsonValueKind.String
            ? maskProp.GetString()
            : null;
        var type = element.GetProperty("type").GetString() ?? string.Empty;
        var subtype = element.TryGetProperty("subtype", out var subProp) && subProp.ValueKind == JsonValueKind.String
            ? subProp.GetString()
            : null;
        return new PlaidAccountDto(accountId, name, mask, type, subtype);
    }

    private static PlaidTransactionDto ReadTransaction(JsonElement element)
    {
        var transactionId = element.GetProperty("transaction_id").GetString() ?? string.Empty;
        var accountId = element.GetProperty("account_id").GetString() ?? string.Empty;
        var amount = element.GetProperty("amount").GetDecimal();

        // Plaid returns ISO date strings (YYYY-MM-DD); use DateTime.Parse for tolerance.
        var dateString = element.GetProperty("date").GetString() ?? string.Empty;
        var date = DateTime.Parse(dateString, System.Globalization.CultureInfo.InvariantCulture);

        var merchantName = element.TryGetProperty("merchant_name", out var merchantProp) && merchantProp.ValueKind == JsonValueKind.String
            ? merchantProp.GetString()
            : null;
        var name = element.GetProperty("name").GetString() ?? string.Empty;
        var pending = element.TryGetProperty("pending", out var pendingProp) && pendingProp.GetBoolean();

        string? pfcPrimary = null;
        if (element.TryGetProperty("personal_finance_category", out var pfcProp) && pfcProp.ValueKind == JsonValueKind.Object)
        {
            if (pfcProp.TryGetProperty("primary", out var primaryProp) && primaryProp.ValueKind == JsonValueKind.String)
                pfcPrimary = primaryProp.GetString();
        }

        return new PlaidTransactionDto(transactionId, accountId, amount, date, merchantName, name, pending, pfcPrimary);
    }
}
