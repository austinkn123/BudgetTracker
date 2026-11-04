using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Webull;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Domain.Accessors;

/// <summary>
/// HttpClient accessor for Webull's OpenAPI. Signs every request with HMAC keyed by the App Secret, which is never sent.
/// Only the six allowlisted (method, path) pairs can be sent. Every failure leaves as <see cref="WebullApiException"/>,
/// except programmer errors (a request off the allowlist, or Webull not configured), which throw
/// <see cref="InvalidOperationException"/>. No business logic, no persistence.
/// </summary>
public class WebullAccessor : IWebullAccessor, IWebullOrderHistoryAccessor
{
    /// <summary>
    /// Sent as <c>x-signature-algorithm</c>; selects HMAC-SHA256 with a SHA-256 body digest, as the official Python SDK
    /// hard-codes. The docs' HMAC-SHA1/MD5 pairing is also computed by <see cref="ComputeSignature"/>; flip this
    /// constant if the sandbox rejects SHA-256 signatures.
    /// </summary>
    public const string SignatureAlgorithm = "HMAC-SHA256";

    private const string CreateTokenPath = "/auth/tokens/create";
    private const string CheckTokenPath = "/auth/tokens/check";
    private const string AccountsListPath = "/trading/accounts/list";
    private const string BalancesPath = "/trading/assets/balances/get";
    private const string PositionsPath = "/trading/assets/positions/list";
    private const string OrderHistoryPath = "/trading/orders/historical-orders/list";

    private static readonly HashSet<(HttpMethod, string)> AllowedRequests =
    [
        (HttpMethod.Post, CreateTokenPath),
        (HttpMethod.Post, CheckTokenPath),
        (HttpMethod.Get, AccountsListPath),
        (HttpMethod.Get, BalancesPath),
        (HttpMethod.Get, PositionsPath),
        (HttpMethod.Get, OrderHistoryPath)
    ];

    /// <summary>Webull allows ~2 requests per 2 seconds per endpoint; a little over a second between calls stays under it.</summary>
    private static readonly TimeSpan RequestSpacing = TimeSpan.FromSeconds(1.1);

    /// <summary>
    /// Order History got a 429 at 1.1 s spacing in production (2026-10-27), so its pages are spaced past the whole 2 s
    /// window. A history is a few pages, so speed does not matter.
    /// </summary>
    private static readonly TimeSpan OrderPageSpacing = TimeSpan.FromSeconds(2.1);

    // Matches Python's json.dumps(separators=(',', ':')): Webull's docs warn that escaped <, > and & break the body digest.
    private static readonly JsonSerializerOptions BodyJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly HttpClient _http;
    private readonly WebullOptions _options;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private bool _orderPageRequested;

    /// <summary>Initializes the accessor. Requests go to <see cref="WebullOptions.BaseUrl"/>, whose host is also signed.</summary>
    /// <param name="http">The HTTP client; its BaseAddress is ignored.</param>
    /// <param name="options">Webull keys and base URL.</param>
    /// <param name="delay">Waits between paced calls; defaults to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</param>
    public WebullAccessor(HttpClient http, IOptions<WebullOptions> options, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _http = http;
        _options = options.Value;
        _delay = delay ?? Task.Delay;
    }

    /// <summary>
    /// Webull's request signature: sort <paramref name="signedParams"/> ordinally into <c>k=v&amp;…</c> (raw values), build
    /// <c>path&amp;str1</c>, append <c>&amp;</c> plus the uppercase hex body digest when there is a body, percent-encode
    /// it all (<see cref="Uri.EscapeDataString"/>, uppercase hex), HMAC it with <c>appSecret + "&amp;"</c>, and Base64 it.
    /// The hash pair follows <paramref name="signedParams"/>' <c>x-signature-algorithm</c>.
    /// </summary>
    /// <param name="path">The request path, without query.</param>
    /// <param name="signedParams">Query parameters plus the signed headers and <c>host</c>.</param>
    /// <param name="body">The exact body sent, or null when there is none.</param>
    /// <param name="appSecret">The App Secret.</param>
    /// <returns>The <c>x-signature</c> value.</returns>
    /// <exception cref="ArgumentException">The algorithm is neither HMAC-SHA1 nor HMAC-SHA256.</exception>
    public static string ComputeSignature(string path, IReadOnlyDictionary<string, string> signedParams, string? body, string appSecret)
    {
        var sha1 = signedParams["x-signature-algorithm"] switch
        {
            "HMAC-SHA1" => true,
            "HMAC-SHA256" => false,
            var other => throw new ArgumentException($"Unsupported signature algorithm '{other}'.", nameof(signedParams))
        };

        var str1 = string.Join('&', signedParams.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
        var toSign = $"{path}&{str1}";
        if (body is not null)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            toSign += "&" + Convert.ToHexString(sha1 ? MD5.HashData(bodyBytes) : SHA256.HashData(bodyBytes));
        }

        var key = Encoding.UTF8.GetBytes(appSecret + "&");
        var data = Encoding.UTF8.GetBytes(Uri.EscapeDataString(toSign));
        return Convert.ToBase64String(sha1 ? HMACSHA1.HashData(key, data) : HMACSHA256.HashData(key, data));
    }

    /// <summary>Throws unless (<paramref name="method"/>, <paramref name="path"/>) is one of the six allowlisted Webull calls.</summary>
    /// <exception cref="InvalidOperationException">The request is not allowlisted.</exception>
    public static void EnsureAllowed(HttpMethod method, string path)
    {
        if (!AllowedRequests.Contains((method, path)))
            throw new InvalidOperationException($"Webull request {method} {path} is not allowlisted.");
    }

    /// <inheritdoc />
    public async Task<WebullToken> CreateTokenAsync(CancellationToken cancellationToken = default) =>
        await SendAsync(HttpMethod.Post, CreateTokenPath, "{}", null, null, ReadToken, cancellationToken);

    /// <inheritdoc />
    public async Task<WebullTokenStatus> CheckTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);
        var body = JsonSerializer.Serialize(new { token = accessToken }, BodyJson);
        var token = await SendAsync(HttpMethod.Post, CheckTokenPath, body, null, null, ReadToken, cancellationToken);
        return token.Status;
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        string? body,
        IReadOnlyList<(string Name, string Value)>? query,
        string? accessToken,
        Func<JsonElement, T> read,
        CancellationToken cancellationToken)
    {
        EnsureAllowed(method, path);
        if (!_options.IsConfigured)
            throw new InvalidOperationException("Webull is not configured; refusing to send.");

        using var request = BuildSignedRequest(method, path, body, query ?? [], accessToken);
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw Classify(path, response.StatusCode, content);

            using var doc = JsonDocument.Parse(content);
            return read(doc.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Transport failure or HttpClient timeout.
            throw new WebullApiException(WebullErrorKind.Unreachable, null, $"Webull {path} failed: no response.", null, ex);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or OverflowException or ArgumentOutOfRangeException
                                       or InvalidOperationException or KeyNotFoundException)
        {
            // The body is not the documented shape. Parser messages quote the body, which may echo the token, so the
            // inner exception is dropped and only its type is kept.
            throw new WebullApiException(WebullErrorKind.Unreachable, null,
                $"Webull {path} returned an unreadable response ({ex.GetType().Name}).", null);
        }
    }

    /// <summary>Every query parameter is signed with its raw value and percent-encoded in the URI (a pagination key carries '=').</summary>
    private HttpRequestMessage BuildSignedRequest(
        HttpMethod method, string path, string? body, IReadOnlyList<(string Name, string Value)> query, string? accessToken)
    {
        var baseUri = new Uri(_options.BaseUrl);
        var signed = new Dictionary<string, string>
        {
            ["x-app-key"] = _options.AppKey,
            ["x-timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["x-signature-algorithm"] = SignatureAlgorithm,
            ["x-signature-version"] = "1.0",
            ["x-signature-nonce"] = Guid.NewGuid().ToString("N")
        };

        var queryString = string.Join('&', query.Select(p => $"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(p.Value)}"));
        var request = new HttpRequestMessage(method, new Uri(baseUri, query.Count == 0 ? path : $"{path}?{queryString}"));
        foreach (var (name, value) in signed)
            request.Headers.Add(name, value);

        foreach (var (name, value) in query)
            signed[name] = value;
        signed["host"] = baseUri.Host;

        request.Headers.Add("x-signature", ComputeSignature(path, signed, body, _options.AppSecret));
        request.Headers.Add("x-version", "v3");
        if (accessToken is not null)
            request.Headers.Add("x-access-token", accessToken);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    private static WebullApiException Classify(string path, HttpStatusCode status, string content)
    {
        string? errorCode = null;
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error_code", out var code) && code.ValueKind == JsonValueKind.String)
                errorCode = code.GetString();
        }
        catch (JsonException)
        {
            // Not JSON (e.g. a gateway HTML page): classify on status alone.
        }

        var kind = errorCode == "INVALID_TOKEN" ? WebullErrorKind.TokenRejected
            : status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? WebullErrorKind.KeyRejected
            : WebullErrorKind.Unreachable;

        return new WebullApiException(kind, errorCode,
            $"Webull {path} failed with HTTP {(int)status}: {errorCode ?? "no error_code"}.", status);
    }

    private static WebullToken ReadToken(JsonElement root)
    {
        var status = root.GetProperty("status").GetString() switch
        {
            "PENDING" => WebullTokenStatus.Pending,
            "NORMAL" => WebullTokenStatus.Normal,
            "EXPIRED" => WebullTokenStatus.Expired,
            "INVALID" => WebullTokenStatus.Invalid,
            _ => throw new FormatException("Unknown Webull token status.")
        };
        var token = root.GetProperty("token").GetString();
        if (string.IsNullOrEmpty(token))
            throw new FormatException("Webull returned no token.");
        return new WebullToken(token, status);
    }

    private static List<(string AccountId, string? Label, string Type)> ReadAccounts(JsonElement root) =>
        root.EnumerateArray()
            .Select(a => (
                a.GetProperty("account_id").GetString() ?? throw new FormatException("Webull account has no account_id."),
                ReadOptionalString(a, "account_label"),
                a.GetProperty("account_type").GetString() ?? string.Empty))
            .ToList();

    /// <summary>
    /// One account's balance, or null when Webull reports no total (an EVENTS_CASH account sends it empty): such an account
    /// is left out rather than shown as $0. A total that is present but not a number is still an unreadable response.
    /// </summary>
    private static WebullBalance? ReadBalance(JsonElement root, string accountId, string? label, string type)
    {
        var total = root.TryGetProperty("total_net_liquidation_value", out var totalProp) ? totalProp : default;
        if (total.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            || total.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(total.GetString()))
            return null;

        var cash = ReadOptionalString(root, "total_cash_balance");
        return new WebullBalance(
            accountId,
            label,
            type,
            ParseAmount(total.GetString()),
            string.IsNullOrEmpty(cash) ? null : ParseAmount(cash),
            root.GetProperty("total_asset_currency").GetString() ?? string.Empty);
    }

    private static decimal ParseAmount(string? value) =>
        decimal.Parse(value ?? throw new FormatException("Missing amount."), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static string? ReadOptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebullAccountPortfolio>> GetPortfolioAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);
        var accounts = await SendAsync(HttpMethod.Get, AccountsListPath, null, null, accessToken, ReadAccounts, cancellationToken);

        var portfolios = new List<WebullAccountPortfolio>(accounts.Count);
        foreach (var (accountId, label, type) in accounts)
        {
            await _delay(RequestSpacing, cancellationToken);
            var portfolio = await SendAsync(HttpMethod.Get, BalancesPath, null, [("account_id", accountId)], accessToken,
                root => ReadPortfolioBalance(root, accountId, label, type), cancellationToken);
            if (portfolio is null)
                continue; // no total (EVENTS_CASH): left out, so its positions are not fetched either

            await _delay(RequestSpacing, cancellationToken);
            var positions = await SendAsync(HttpMethod.Get, PositionsPath, null, [("account_id", accountId)], accessToken, ReadPositions, cancellationToken);
            portfolios.Add(portfolio with { Positions = positions });
        }
        return portfolios;
    }

    /// <summary>The v1 balance plus the three totals v1 discarded, with no positions yet; null when the balance is skipped.</summary>
    private static WebullAccountPortfolio? ReadPortfolioBalance(JsonElement root, string accountId, string? label, string type) =>
        ReadBalance(root, accountId, label, type) is { } balance
            ? new WebullAccountPortfolio(
                balance,
                ReadOptionalAmount(root, "total_market_value"),
                ReadOptionalAmount(root, "total_day_profit_loss"),
                ReadOptionalAmount(root, "total_unrealized_profit_loss"),
                [])
            : null;

    /// <summary>v3 positions: a bare array. Option legs, strategy and event outcome are not read.</summary>
    private static List<WebullPosition> ReadPositions(JsonElement root) =>
        root.EnumerateArray()
            .Select(p => new WebullPosition(
                p.GetProperty("symbol").GetString() ?? throw new FormatException("Webull position has no symbol."),
                p.GetProperty("instrument_type").GetString() ?? throw new FormatException("Webull position has no instrument_type."),
                ReadOptionalAmount(p, "quantity"),
                ReadOptionalAmount(p, "last_price"),
                ReadOptionalAmount(p, "cost_price"),
                ReadOptionalAmount(p, "unrealized_profit_loss"),
                ReadOptionalString(p, "currency") ?? string.Empty))
            .ToList();

    /// <summary>
    /// A number Webull may send empty: missing, null, or a blank string is null (never 0). A numeric string or a JSON number
    /// is parsed with the invariant culture. Anything else throws, which surfaces as an unreadable response.
    /// </summary>
    private static decimal? ReadOptionalAmount(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String when string.IsNullOrWhiteSpace(prop.GetString()) => null,
            JsonValueKind.String => ParseAmount(prop.GetString()),
            JsonValueKind.Number => prop.GetDecimal(),
            _ => throw new FormatException($"Webull {name} is not a number.")
        };
    }

    /// <inheritdoc />
    public async Task<WebullOrderPage> GetOrderPageAsync(
        string accessToken,
        string accountId,
        WebullOrderWindow window,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);
        ArgumentException.ThrowIfNullOrEmpty(accountId);
        ArgumentNullException.ThrowIfNull(window);

        List<(string, string)> query =
            [("account_id", accountId), ("start_time", FormatOrderTime(window.Start)), ("end_time", FormatOrderTime(window.End))];
        if (!string.IsNullOrEmpty(window.PaginationKey))
            query.Add(("pagination_key", window.PaginationKey));

        // Spaced only from this instance's previous order call. Whether Webull's budget is shared with the portfolio
        // endpoints is unknown; the trades manager retries a 429 either way.
        if (_orderPageRequested)
            await _delay(OrderPageSpacing, cancellationToken);
        _orderPageRequested = true;

        return await SendAsync(HttpMethod.Get, OrderHistoryPath, null, query, accessToken,
            root => ReadOrderPage(root, accountId), cancellationToken);
    }

    private static string FormatOrderTime(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>Every <c>orders[]</c> entry of every combo group. A missing or null <c>data</c> or <c>orders</c> is empty.</summary>
    private static WebullOrderPage ReadOrderPage(JsonElement root, string accountId)
    {
        var orders = new List<WebullOrder>();
        if (root.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null)
        {
            foreach (var group in data.EnumerateArray())
            {
                if (group.TryGetProperty("orders", out var groupOrders) && groupOrders.ValueKind != JsonValueKind.Null)
                    orders.AddRange(groupOrders.EnumerateArray().Select(o => ReadOrder(o, accountId)));
            }
        }
        return new WebullOrderPage(orders, ReadOptionalText(root, "pagination_key"));
    }

    private static WebullOrder ReadOrder(JsonElement order, string accountId) => new(
        accountId,
        ReadRequiredText(order, "order_id"),
        ReadRequiredText(order, "symbol"),
        ReadOptionalText(order, "instrument_type"),
        ReadRequiredText(order, "side"),
        ReadRequiredText(order, "status"),
        ReadOptionalAmount(order, "filled_quantity"),
        ReadOptionalAmount(order, "filled_price"),
        ReadOptionalTime(order, "place_time_at", "place_time") ?? throw new FormatException("Webull order has no placement time."),
        ReadOptionalTime(order, "filled_time_at", "filled_time"));

    /// <summary>Missing, null or blank is null; a string is kept as sent; any other JSON kind is unreadable.</summary>
    private static string? ReadOptionalText(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.GetString() is { } text && !string.IsNullOrWhiteSpace(text) ? text : null;

    private static string ReadRequiredText(JsonElement element, string name) =>
        ReadOptionalText(element, name) ?? throw new FormatException($"Webull order has no {name}.");

    /// <summary>
    /// An ISO-8601 time (offset <c>Z</c>, <c>+0000</c> or <c>+00:00</c>; none means UTC), else the epoch-milliseconds
    /// fallback, else null. Always returned with kind UTC.
    /// </summary>
    private static DateTime? ReadOptionalTime(JsonElement element, string isoName, string epochMsName)
    {
        if (ReadOptionalText(element, isoName) is { } iso)
            return DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).UtcDateTime;

        return ReadOptionalAmount(element, epochMsName) is { } ms
            ? DateTimeOffset.FromUnixTimeMilliseconds(decimal.ToInt64(ms)).UtcDateTime
            : null;
    }
}
