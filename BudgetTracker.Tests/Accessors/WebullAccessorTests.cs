using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Webull;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Tests.Accessors;

/// <summary>
/// Tests for <see cref="WebullAccessor"/>: the signer against Webull's published worked example, the path allowlist,
/// request shape (headers, exact body bytes, query), response parsing, pacing, and error classification. Uses a fake
/// <see cref="HttpMessageHandler"/> that records every request.
/// </summary>
public class WebullAccessorTests
{
    private const string AppKey = "app-key-abc";
    private const string AppSecret = "app-secret-xyz";
    private const string AccessToken = "tok-secret-123";

    private const string AccountsJson = """
        [{"account_id":"ACC1","account_label":"Individual Cash","account_type":"CASH","account_number":"5MV001"},
         {"account_id":"ACC2","account_type":"MARGIN"}]
        """;

    private const string Acc1BalanceJson = """
        {"total_asset_currency":"USD","total_cash_balance":"485705.0","total_net_liquidation_value":"1234567.89","account_currency_assets":[]}
        """;

    private const string Acc2BalanceJson = """
        {"total_asset_currency":"USD","total_cash_balance":"","total_net_liquidation_value":"10.5"}
        """;

    /// <summary>Records each request (with its body read as sent) and answers via <paramref name="respond"/>.</summary>
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Sent { get; } = [];
        public List<string> Log { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Sent.Add((request, body));
            Log.Add($"send {request.RequestUri!.PathAndQuery}");
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static FakeHandler Returning(HttpStatusCode status, string json) => new(_ => Json(status, json));

    private static WebullAccessor BuildAccessor(
        FakeHandler handler,
        string appKey = AppKey,
        string appSecret = AppSecret,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        string baseUrl = "https://api.sandbox.webull.com")
    {
        var options = Options.Create(new WebullOptions { AppKey = appKey, AppSecret = appSecret, BaseUrl = baseUrl });
        return new WebullAccessor(new HttpClient(handler), options, delay ?? ((_, _) => Task.CompletedTask));
    }

    private static string Header(HttpRequestMessage request, string name) => request.Headers.GetValues(name).Single();

    [Fact]
    public void TypedClientRegistration_ResolvesWithDefaultDelay()
    {
        // Mirrors Program.cs. The delay parameter must stay optional, or DI fails on the first Webull request.
        var services = new ServiceCollection();
        services.Configure<WebullOptions>(_ => { });
        services.AddHttpClient<IWebullAccessor, WebullAccessor>(client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddHttpClient<IWebullOrderHistoryAccessor, WebullAccessor>(client => client.Timeout = TimeSpan.FromSeconds(10));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<WebullAccessor>(provider.GetRequiredService<IWebullAccessor>());
        Assert.IsType<WebullAccessor>(provider.GetRequiredService<IWebullOrderHistoryAccessor>());
    }

    // ── Signer ──────────────────────────────────────────────────────────────

    private static Dictionary<string, string> DocsExampleParams(string algorithm) => new()
    {
        ["a1"] = "webull",
        ["a2"] = "123",
        ["a3"] = "xxx",
        ["q1"] = "yyy",
        ["x-app-key"] = "776da210ab4a452795d74e726ebd74b6",
        ["x-timestamp"] = "2022-01-04T03:55:31Z",
        ["x-signature-version"] = "1.0",
        ["x-signature-algorithm"] = algorithm,
        ["x-signature-nonce"] = "48ef5afed43d4d91ae514aaeafbc29ba",
        ["host"] = "api.webull.com"
    };

    private const string DocsExampleBody = """{"k1":123,"k2":"this is the api request body","k3":true,"k4":{"foo":[1,2]}}""";
    private const string DocsExampleSecret = "0f50a2e853334a9aae1a783bee120c1f";

    [Fact]
    public void ComputeSignature_DocsWorkedExample_MatchesPublishedSignature()
    {
        // https://developer.webull.com/apis/docs/authentication/signature, "Worked Example" (HMAC-SHA1, MD5 body digest).
        var signature = WebullAccessor.ComputeSignature(
            "/trade/place_order", DocsExampleParams("HMAC-SHA1"), DocsExampleBody, DocsExampleSecret);

        Assert.Equal("kvlS6opdZDhEBo5jq40nHYXaLvM=", signature);
    }

    [Fact]
    public void ComputeSignature_Sha256_MatchesPythonSdkAlgorithm()
    {
        // The docs' inputs run through the official Python SDK's composer (HMAC-SHA256, SHA-256 body digest).
        var signature = WebullAccessor.ComputeSignature(
            "/trade/place_order", DocsExampleParams("HMAC-SHA256"), DocsExampleBody, DocsExampleSecret);

        Assert.Equal("WmKFpDtQMSUhCYjmgA66EX5dQo+pS4qOwu3Kl0tb6KU=", signature);
    }

    [Fact]
    public void ComputeSignature_NoBody_OmitsBodyDigest()
    {
        // Same docs inputs with no body (str3 = path & str1), computed with Python's urllib.parse.quote(safe='').
        var signature = WebullAccessor.ComputeSignature(
            "/trade/place_order", DocsExampleParams("HMAC-SHA1"), body: null, DocsExampleSecret);

        Assert.Equal("L1d4zL2x6UmLQ5ja7jnTOjFYjEk=", signature);
    }

    [Fact]
    public void ComputeSignature_UnknownAlgorithm_Throws()
    {
        Assert.Throws<ArgumentException>(() => WebullAccessor.ComputeSignature(
            "/trade/place_order", DocsExampleParams("HMAC-MD5"), null, DocsExampleSecret));
    }

    [Fact]
    public void ComputeSignature_SeveralQueryParams_SignsEachRawValue()
    {
        // Order history's query (a pagination key ending in '=') with the docs' headers, run through Python's
        // urllib.parse.quote(safe='') composer (HMAC-SHA256, no body).
        var signedParams = DocsExampleParams("HMAC-SHA256");
        foreach (var key in new[] { "a1", "a2", "a3", "q1" })
            signedParams.Remove(key);
        signedParams["account_id"] = "ACC1";
        signedParams["start_time"] = "2025-01-01T00:00:00.000Z";
        signedParams["end_time"] = "2025-12-31T23:59:59.999Z";
        signedParams["pagination_key"] = "eyJ2IjoxLCJwIjoyfQ==";

        var signature = WebullAccessor.ComputeSignature("/trading/orders/historical-orders/list", signedParams, null, DocsExampleSecret);

        Assert.Equal("LM+LDqaS6mtKdo79PyfTY3ENJLzNi3tUlQmZ6tHEoQw=", signature);
    }

    // ── Path allowlist (AC 18: read-only surface) ───────────────────────────

    [Fact]
    public void AllowedRequests_AreExactlyTheSixReadOnlyCalls()
    {
        var field = typeof(WebullAccessor).GetField("AllowedRequests", BindingFlags.NonPublic | BindingFlags.Static);
        var allowed = Assert.IsAssignableFrom<IEnumerable<(HttpMethod, string)>>(field!.GetValue(null));

        Assert.Equal(
            new HashSet<(HttpMethod, string)>
            {
                (HttpMethod.Post, "/auth/tokens/create"),
                (HttpMethod.Post, "/auth/tokens/check"),
                (HttpMethod.Get, "/trading/accounts/list"),
                (HttpMethod.Get, "/trading/assets/balances/get"),
                (HttpMethod.Get, "/trading/assets/positions/list"),
                (HttpMethod.Get, "/trading/orders/historical-orders/list")
            },
            allowed.ToHashSet());
    }

    [Fact]
    public void IWebullAccessor_ExposesOnlyTokenAndPortfolioCalls()
    {
        Assert.Equal(
            ["CheckTokenAsync", "CreateTokenAsync", "GetPortfolioAsync"],
            typeof(IWebullAccessor).GetMethods().Select(m => m.Name).Order());
    }

    [Fact]
    public void IWebullOrderHistoryAccessor_ExposesOnlyTheOrderPageCall_AndCannotCreateTokens()
    {
        Assert.Equal(["GetOrderPageAsync"], typeof(IWebullOrderHistoryAccessor).GetMethods().Select(m => m.Name));
        Assert.False(typeof(IWebullOrderHistoryAccessor).IsAssignableTo(typeof(IWebullAccessor)));
        Assert.True(typeof(WebullAccessor).IsAssignableTo(typeof(IWebullOrderHistoryAccessor)));
    }

    [Theory]
    [InlineData("POST", "/auth/tokens/create")]
    [InlineData("POST", "/auth/tokens/check")]
    [InlineData("GET", "/trading/accounts/list")]
    [InlineData("GET", "/trading/assets/balances/get")]
    [InlineData("GET", "/trading/assets/positions/list")]
    [InlineData("GET", "/trading/orders/historical-orders/list")]
    public void EnsureAllowed_AllowlistedRequest_DoesNotThrow(string method, string path)
    {
        WebullAccessor.EnsureAllowed(new HttpMethod(method), path);
    }

    [Theory]
    [InlineData("GET", "/auth/tokens/create")]
    [InlineData("POST", "/trading/accounts/list")]
    [InlineData("POST", "/trading/order/place")]
    [InlineData("DELETE", "/auth/tokens/check")]
    [InlineData("GET", "/trading/accounts/list/")]
    [InlineData("GET", "/Trading/Accounts/List")]
    [InlineData("GET", "/trading/assets/balances/get?account_id=1")]
    [InlineData("POST", "/trading/assets/positions/list")]
    [InlineData("GET", "/trading/assets/positions/list?account_id=1")]
    [InlineData("POST", "/trading/orders/historical-orders/list")]
    [InlineData("GET", "/trading/orders/historical-orders/list?account_id=1")]
    [InlineData("GET", "/trading/orders/open-orders/list")]
    [InlineData("GET", "/trading/orders/detail")]
    [InlineData("GET", "")]
    public void EnsureAllowed_AnyOtherRequest_ThrowsInvalidOperation(string method, string path)
    {
        Assert.Throws<InvalidOperationException>(() => WebullAccessor.EnsureAllowed(new HttpMethod(method), path));
    }

    [Theory]
    [InlineData("", AppSecret)]
    [InlineData(AppKey, "")]
    [InlineData("  ", "  ")]
    public async Task AnyCall_NotConfigured_ThrowsWithoutSending(string appKey, string appSecret)
    {
        var handler = Returning(HttpStatusCode.OK, """{"token":"t","expires":1,"status":"NORMAL"}""");
        var accessor = BuildAccessor(handler, appKey, appSecret);

        await Assert.ThrowsAsync<InvalidOperationException>(() => accessor.CreateTokenAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => accessor.CheckTokenAsync(AccessToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => accessor.GetPortfolioAsync(AccessToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => accessor.GetOrderPageAsync(AccessToken, "ACC1", OrderWindow));
        Assert.Empty(handler.Sent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task TokenCalls_NullOrEmptyToken_ThrowArgumentException(string? token)
    {
        var handler = Returning(HttpStatusCode.OK, "{}");
        var accessor = BuildAccessor(handler);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => accessor.CheckTokenAsync(token!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => accessor.GetPortfolioAsync(token!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => accessor.GetOrderPageAsync(token!, "ACC1", OrderWindow));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => accessor.GetOrderPageAsync(AccessToken, token!, OrderWindow));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => accessor.GetOrderPageAsync(AccessToken, "ACC1", null!));
        Assert.Empty(handler.Sent);
    }

    // ── Request shape ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateToken_PostsEmptyJsonObject_AndReturnsToken()
    {
        var handler = Returning(HttpStatusCode.OK, """{"token":"new-token","expires":1767225600000,"status":"NORMAL"}""");

        var token = await BuildAccessor(handler).CreateTokenAsync();

        var (request, body) = Assert.Single(handler.Sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.sandbox.webull.com/auth/tokens/create", request.RequestUri!.ToString());
        Assert.Equal("{}", body);
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal(new WebullToken("new-token", WebullTokenStatus.Normal), token);
    }

    [Fact]
    public async Task CheckToken_PostsTokenAsExactJsonBody()
    {
        var handler = Returning(HttpStatusCode.OK, """{"token":"tok-secret-123","expires":1,"status":"PENDING"}""");

        await BuildAccessor(handler).CheckTokenAsync("tok+/=<&>");

        var (request, body) = Assert.Single(handler.Sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/auth/tokens/check", request.RequestUri!.AbsolutePath);
        // Unescaped, compact: the server hashes the body as Python's json.dumps would write it.
        Assert.Equal("""{"token":"tok+/=<&>"}""", body);
    }

    [Theory]
    [InlineData("PENDING", WebullTokenStatus.Pending)]
    [InlineData("NORMAL", WebullTokenStatus.Normal)]
    [InlineData("EXPIRED", WebullTokenStatus.Expired)]
    [InlineData("INVALID", WebullTokenStatus.Invalid)]
    public async Task CheckToken_MapsEveryStatus(string wire, WebullTokenStatus expected)
    {
        var handler = Returning(HttpStatusCode.OK, $$"""{"token":"t","expires":1,"status":"{{wire}}"}""");

        Assert.Equal(expected, await BuildAccessor(handler).CheckTokenAsync(AccessToken));
    }

    [Fact]
    public async Task SignedPost_SendsSigningHeaders_AndSignatureCoversExactBody()
    {
        var handler = Returning(HttpStatusCode.OK, """{"token":"t","expires":1,"status":"NORMAL"}""");

        await BuildAccessor(handler).CheckTokenAsync(AccessToken);

        var (request, body) = Assert.Single(handler.Sent);
        Assert.Equal(AppKey, Header(request, "x-app-key"));
        Assert.Equal(WebullAccessor.SignatureAlgorithm, Header(request, "x-signature-algorithm"));
        Assert.Equal("1.0", Header(request, "x-signature-version"));
        Assert.Equal("v3", Header(request, "x-version"));
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$", Header(request, "x-timestamp"));
        Assert.Matches("^[0-9a-f]{32}$", Header(request, "x-signature-nonce"));

        var expected = WebullAccessor.ComputeSignature("/auth/tokens/check", SignedParams(request), body, AppSecret);
        Assert.Equal(expected, Header(request, "x-signature"));
    }

    [Fact]
    public async Task SignedGet_SignatureCoversQueryParams_AndSendsAccessToken()
    {
        var handler = PortfolioHandler();

        await BuildAccessor(handler).GetPortfolioAsync(AccessToken);

        Assert.All(handler.Sent, sent => Assert.Equal(AccessToken, Header(sent.Request, "x-access-token")));
        var (request, body) = handler.Sent[1]; // ACC1's balance
        Assert.Equal("/trading/assets/balances/get?account_id=ACC1", request.RequestUri!.PathAndQuery);
        Assert.Null(body);
        var signed = SignedParams(request);
        signed["account_id"] = "ACC1";
        var expected = WebullAccessor.ComputeSignature("/trading/assets/balances/get", signed, null, AppSecret);
        Assert.Equal(expected, Header(request, "x-signature"));
    }

    [Fact]
    public async Task EveryRequest_NeverSendsSecret_AndUsesFreshNonce()
    {
        var handler = PortfolioHandler();

        await BuildAccessor(handler).GetPortfolioAsync(AccessToken);

        Assert.All(handler.Sent, sent =>
        {
            Assert.False(sent.Request.Headers.Contains("x-app-secret"));
            Assert.DoesNotContain(AppSecret, string.Join("|", sent.Request.Headers.SelectMany(h => h.Value)));
        });
        Assert.Equal(5, handler.Sent.Select(s => Header(s.Request, "x-signature-nonce")).Distinct().Count());
    }

    // ── AC 19: no secret or token leaks ─────────────────────────────────────

    /// <summary>Answers every allowlisted call with a valid body.</summary>
    private static FakeHandler AllCallsHandler() =>
        new(request => request.RequestUri!.AbsolutePath.StartsWith("/auth/")
            ? Json(HttpStatusCode.OK, $$"""{"token":"{{AccessToken}}","expires":1,"status":"NORMAL"}""")
            : TradingResponse(request));

    private static HttpResponseMessage TradingResponse(HttpRequestMessage request) =>
        request.RequestUri!.AbsolutePath switch
        {
            "/trading/accounts/list" => Json(HttpStatusCode.OK, AccountsJson),
            "/trading/assets/positions/list" => Json(HttpStatusCode.OK, PositionsJson),
            "/trading/orders/historical-orders/list" => Json(HttpStatusCode.OK, OrderPageJson),
            _ => Json(HttpStatusCode.OK, Acc1BalanceJson)
        };

    [Theory]
    [InlineData("create")]
    [InlineData("check")]
    [InlineData("portfolio")]
    [InlineData("orders")]
    public async Task EveryRequestType_NeverCarriesTheSecret_InHeadersUriOrBody(string call)
    {
        var handler = AllCallsHandler();
        var accessor = BuildAccessor(handler);

        await (call switch
        {
            "create" => accessor.CreateTokenAsync(),
            "check" => accessor.CheckTokenAsync(AccessToken),
            "orders" => accessor.GetOrderPageAsync(AccessToken, "ACC1", OrderWindow with { PaginationKey = "pk=1" }),
            _ => (Task)accessor.GetPortfolioAsync(AccessToken)
        });

        Assert.NotEmpty(handler.Sent);
        Assert.All(handler.Sent, sent =>
        {
            var headers = sent.Request.Headers.Concat(sent.Request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>());
            Assert.DoesNotContain(headers, h => h.Key.Equals("x-app-secret", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(AppSecret, string.Join("|", headers.SelectMany(h => h.Value)));
            Assert.DoesNotContain(AppSecret, sent.Request.RequestUri!.ToString());
            Assert.DoesNotContain(AppSecret, sent.Body ?? string.Empty);
        });
    }

    [Fact]
    public void WebullToken_ToString_DoesNotContainToken()
    {
        var text = new WebullToken(AccessToken, WebullTokenStatus.Normal).ToString();

        Assert.DoesNotContain(AccessToken, text);
        Assert.Contains("Normal", text);
    }

    [Theory]
    [InlineData("echo tok-secret-123 app-key-abc app-secret-xyz")]
    [InlineData("tok-secret-123 app-key-abc app-secret-xyz")]
    [InlineData("<html>tok-secret-123 app-key-abc app-secret-xyz</html>")]
    public async Task UnreadableSuccessBody_ExceptionToString_CarriesNoSecrets(string body)
    {
        var handler = Returning(HttpStatusCode.OK, body);

        var ex = await Assert.ThrowsAsync<WebullApiException>(() => BuildAccessor(handler).CheckTokenAsync(AccessToken));

        // System.Text.Json's messages quote the body, so the parser exception must not ride along as InnerException.
        var text = ex.ToString();
        Assert.Contains("unreadable response", ex.Message);
        Assert.DoesNotContain(AccessToken, text);
        Assert.DoesNotContain(AppKey, text);
        Assert.DoesNotContain(AppSecret, text);
    }

    // ── AC 20: environment ──────────────────────────────────────────────────

    [Fact]
    public void WebullOptions_Default_IsSandbox()
    {
        Assert.Equal("https://api.sandbox.webull.com", new WebullOptions().BaseUrl);
    }

    [Fact]
    public async Task Requests_UseConfiguredProductionBaseUrl_AndSignItsHost()
    {
        var handler = Returning(HttpStatusCode.OK, """{"token":"t","expires":1,"status":"NORMAL"}""");

        await BuildAccessor(handler, baseUrl: "https://api.webull.com").CheckTokenAsync(AccessToken);

        var (request, body) = Assert.Single(handler.Sent);
        Assert.Equal("https://api.webull.com/auth/tokens/check", request.RequestUri!.ToString());
        var expected = WebullAccessor.ComputeSignature("/auth/tokens/check", SignedParams(request, "api.webull.com"), body, AppSecret);
        Assert.Equal(expected, Header(request, "x-signature"));
    }

    /// <summary>The headers the server signs, read back from a sent request.</summary>
    private static Dictionary<string, string> SignedParams(HttpRequestMessage request, string host = "api.sandbox.webull.com") => new()
    {
        ["x-app-key"] = Header(request, "x-app-key"),
        ["x-timestamp"] = Header(request, "x-timestamp"),
        ["x-signature-algorithm"] = Header(request, "x-signature-algorithm"),
        ["x-signature-version"] = Header(request, "x-signature-version"),
        ["x-signature-nonce"] = Header(request, "x-signature-nonce"),
        ["host"] = host
    };

    // ── GetPortfolioAsync ───────────────────────────────────────────────────

    // v3 positions: a bare array; every number is a string.
    private const string PositionsJson = """
        [{"position_id":"P1","symbol":"AAPL","quantity":"10","instrument_type":"EQUITY","currency":"USD","last_price":"150.25","cost_price":"120.5","unrealized_profit_loss":"297.50","option_strategy":""},
         {"position_id":"P2","symbol":"BTCUSD","quantity":"0.0123","instrument_type":"CRYPTO","currency":"USD","last_price":"60000.12","cost_price":"50000","unrealized_profit_loss":"-12.3"}]
        """;

    private const string PortfolioAcc1BalanceJson = """
        {"total_asset_currency":"USD","total_cash_balance":"485705.0","total_net_liquidation_value":"1234567.89",
         "total_market_value":"748862.89","total_day_profit_loss":"-1234.5","total_unrealized_profit_loss":"9876.54","account_currency_assets":[]}
        """;

    private static readonly WebullAccountPortfolio Acc1Portfolio = new(
        new WebullBalance("ACC1", "Individual Cash", "CASH", 1234567.89m, 485705.0m, "USD"),
        748862.89m, -1234.5m, 9876.54m,
        [
            new WebullPosition("AAPL", "EQUITY", 10m, 150.25m, 120.5m, 297.50m, "USD"),
            new WebullPosition("BTCUSD", "CRYPTO", 0.0123m, 60000.12m, 50000m, -12.3m, "USD")
        ]);

    /// <summary>ACC1 holds two positions; ACC2's balance and positions are supplied per test.</summary>
    private static FakeHandler PortfolioHandler(
        string acc2BalanceJson = Acc2BalanceJson,
        HttpStatusCode acc2BalanceStatus = HttpStatusCode.OK,
        string acc2PositionsJson = "[]",
        HttpStatusCode acc2PositionsStatus = HttpStatusCode.OK,
        string acc1PositionsJson = PositionsJson) =>
        new(request => request.RequestUri!.PathAndQuery switch
        {
            "/trading/accounts/list" => Json(HttpStatusCode.OK, AccountsJson),
            "/trading/assets/balances/get?account_id=ACC1" => Json(HttpStatusCode.OK, PortfolioAcc1BalanceJson),
            "/trading/assets/positions/list?account_id=ACC1" => Json(HttpStatusCode.OK, acc1PositionsJson),
            "/trading/assets/balances/get?account_id=ACC2" => Json(acc2BalanceStatus, acc2BalanceJson),
            "/trading/assets/positions/list?account_id=ACC2" => Json(acc2PositionsStatus, acc2PositionsJson),
            var other => throw new InvalidOperationException($"unexpected request {other}")
        });

    [Fact]
    public async Task GetPortfolio_ReturnsEachAccountsBalanceTotalsAndPositions()
    {
        var portfolio = await BuildAccessor(PortfolioHandler()).GetPortfolioAsync(AccessToken);

        Assert.Equal(2, portfolio.Count);
        Assert.Equal(Acc1Portfolio.Balance, portfolio[0].Balance);
        Assert.Equal(
            (Acc1Portfolio.TotalMarketValue, Acc1Portfolio.TotalDayProfitLoss, Acc1Portfolio.TotalUnrealizedProfitLoss),
            (portfolio[0].TotalMarketValue, portfolio[0].TotalDayProfitLoss, portfolio[0].TotalUnrealizedProfitLoss));
        Assert.Equal(Acc1Portfolio.Positions, portfolio[0].Positions);

        // ACC2: the three extra totals are absent or empty, so null; no positions is an empty list, not an error.
        Assert.Equal(new WebullBalance("ACC2", null, "MARGIN", 10.5m, null, "USD"), portfolio[1].Balance);
        Assert.Null(portfolio[1].TotalMarketValue);
        Assert.Null(portfolio[1].TotalDayProfitLoss);
        Assert.Null(portfolio[1].TotalUnrealizedProfitLoss);
        Assert.Empty(portfolio[1].Positions);
    }

    [Fact]
    public async Task GetPortfolio_OneListCall_ThenBalanceAndPositionsPerAccount_Paced()
    {
        var handler = PortfolioHandler();
        var delays = new List<TimeSpan>();
        var accessor = BuildAccessor(handler, delay: (span, _) =>
        {
            delays.Add(span);
            handler.Log.Add("delay");
            return Task.CompletedTask;
        });

        await accessor.GetPortfolioAsync(AccessToken);

        Assert.Equal(
            [
                "send /trading/accounts/list",
                "delay", "send /trading/assets/balances/get?account_id=ACC1",
                "delay", "send /trading/assets/positions/list?account_id=ACC1",
                "delay", "send /trading/assets/balances/get?account_id=ACC2",
                "delay", "send /trading/assets/positions/list?account_id=ACC2"
            ],
            handler.Log);
        Assert.All(delays, d => Assert.Equal(TimeSpan.FromSeconds(1.1), d)); // unchanged by the order-history pacing
    }

    [Fact]
    public async Task GetPortfolio_PositionsRequest_IsSignedWithAccountId_AndCarriesToken()
    {
        var handler = PortfolioHandler();

        await BuildAccessor(handler).GetPortfolioAsync(AccessToken);

        var (request, body) = handler.Sent.Single(s => s.Request.RequestUri!.PathAndQuery == "/trading/assets/positions/list?account_id=ACC1");
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Null(body);
        Assert.Equal(AccessToken, Header(request, "x-access-token"));
        var signed = SignedParams(request);
        signed["account_id"] = "ACC1";
        Assert.Equal(WebullAccessor.ComputeSignature("/trading/assets/positions/list", signed, null, AppSecret), Header(request, "x-signature"));
    }

    [Theory]
    // Webull leaves the totals empty for an EVENTS_CASH account; also tolerate the field being blank, null or absent.
    [InlineData("""{"total_asset_currency":"USD","total_cash_balance":"","total_net_liquidation_value":""}""")]
    [InlineData("""{"total_asset_currency":"USD","total_cash_balance":"","total_net_liquidation_value":"  "}""")]
    [InlineData("""{"total_asset_currency":"USD","total_cash_balance":"","total_net_liquidation_value":null}""")]
    [InlineData("""{"total_asset_currency":"USD","total_cash_balance":""}""")]
    public async Task GetPortfolio_EventsCashOrEmptyTotalAccount_SkippedWithNoPositionsCall(string acc2BalanceJson)
    {
        // Never shown as $0 (AC 15): the account is left out, the others still come back.
        var handler = PortfolioHandler(acc2BalanceJson);

        var portfolio = await BuildAccessor(handler).GetPortfolioAsync(AccessToken);

        Assert.Equal(Acc1Portfolio.Balance, Assert.Single(portfolio).Balance);
        Assert.DoesNotContain(handler.Sent, s => s.Request.RequestUri!.PathAndQuery == "/trading/assets/positions/list?account_id=ACC2");
        Assert.Equal(4, handler.Sent.Count);
    }

    [Fact]
    public async Task GetPortfolio_EmptyOrMissingPositionNumbers_AreNull()
    {
        const string positions = """
            [{"position_id":"P1","symbol":"XYZ","quantity":"","instrument_type":"EQUITY","currency":"USD","last_price":null,"unrealized_profit_loss":"  "}]
            """;

        var portfolio = await BuildAccessor(PortfolioHandler(acc1PositionsJson: positions)).GetPortfolioAsync(AccessToken);

        Assert.Equal(new WebullPosition("XYZ", "EQUITY", null, null, null, null, "USD"), Assert.Single(portfolio[0].Positions));
    }

    [Fact]
    public async Task GetPortfolio_ParsesStringNumbersWithInvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var portfolio = await BuildAccessor(PortfolioHandler()).GetPortfolioAsync(AccessToken);

            Assert.Equal(1234567.89m, portfolio[0].Balance.TotalNetLiquidationValue);
            Assert.Equal(150.25m, portfolio[0].Positions[0].LastPrice);
            Assert.Equal(748862.89m, portfolio[0].TotalMarketValue);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task GetPortfolio_NoAccounts_ReturnsEmptyWithoutFurtherCalls()
    {
        var handler = Returning(HttpStatusCode.OK, "[]");

        var portfolio = await BuildAccessor(handler).GetPortfolioAsync(AccessToken);

        Assert.Empty(portfolio);
        Assert.Single(handler.Sent);
    }

    [Theory]
    [InlineData("""{"total_asset_currency":"USD","total_cash_balance":"1","total_net_liquidation_value":"abc"}""")]
    [InlineData("""{"total_asset_currency":"USD","total_cash_balance":"1","total_net_liquidation_value":"12.3.4"}""")]
    [InlineData("""{"total_asset_currency":"USD","total_cash_balance":"abc","total_net_liquidation_value":"1"}""")]
    public async Task GetPortfolio_NonNumericBalanceAmount_ThrowsUnreachable(string acc2BalanceJson)
    {
        var ex = await Assert.ThrowsAsync<WebullApiException>(
            () => BuildAccessor(PortfolioHandler(acc2BalanceJson)).GetPortfolioAsync(AccessToken));

        Assert.Equal(WebullErrorKind.Unreachable, ex.Kind);
    }

    [Fact]
    public async Task GetPortfolio_AnyBalanceCallFails_ThrowsAndReturnsNothing()
    {
        var handler = PortfolioHandler("""{"error_code":"SYSTEM_ERROR"}""", HttpStatusCode.InternalServerError);

        var ex = await Assert.ThrowsAsync<WebullApiException>(() => BuildAccessor(handler).GetPortfolioAsync(AccessToken));

        Assert.Equal(WebullErrorKind.Unreachable, ex.Kind);
    }

    [Theory]
    [InlineData("quantity", "abc")]
    [InlineData("last_price", "1.2.3")]
    [InlineData("cost_price", "x")]
    [InlineData("unrealized_profit_loss", "--")]
    public async Task GetPortfolio_NonNumericPositionValue_ThrowsUnreachable(string field, string garbage)
    {
        var positions = PositionsJson.Replace($"\"{field}\":\"", $"\"{field}\":\"{garbage}");

        var ex = await Assert.ThrowsAsync<WebullApiException>(
            () => BuildAccessor(PortfolioHandler(acc1PositionsJson: positions)).GetPortfolioAsync(AccessToken));

        Assert.Equal(WebullErrorKind.Unreachable, ex.Kind);
    }

    [Theory]
    [InlineData("total_market_value")]
    [InlineData("total_day_profit_loss")]
    [InlineData("total_unrealized_profit_loss")]
    public async Task GetPortfolio_NonNumericBalanceTotal_ThrowsUnreachable(string field)
    {
        var acc2 = $$"""{"total_asset_currency":"USD","total_cash_balance":"1","total_net_liquidation_value":"2","{{field}}":"n/a"}""";

        var ex = await Assert.ThrowsAsync<WebullApiException>(
            () => BuildAccessor(PortfolioHandler(acc2)).GetPortfolioAsync(AccessToken));

        Assert.Equal(WebullErrorKind.Unreachable, ex.Kind);
    }

    [Theory]
    [InlineData(500, """{"error_code":"SYSTEM_ERROR"}""", WebullErrorKind.Unreachable)]
    [InlineData(401, """{"error_code":"UNAUTHORIZED"}""", WebullErrorKind.KeyRejected)]
    [InlineData(417, """{"error_code":"INVALID_TOKEN"}""", WebullErrorKind.TokenRejected)]
    public async Task GetPortfolio_AnyPositionsCallFails_ThrowsAndReturnsNothing(int status, string json, WebullErrorKind kind)
    {
        var handler = PortfolioHandler(acc2PositionsJson: json, acc2PositionsStatus: (HttpStatusCode)status);

        var ex = await Assert.ThrowsAsync<WebullApiException>(() => BuildAccessor(handler).GetPortfolioAsync(AccessToken));

        Assert.Equal(kind, ex.Kind);
    }

    [Theory]
    [InlineData("tok-secret-123 app-key-abc app-secret-xyz")]
    [InlineData("""[{"symbol":"AAPL","instrument_type":"EQUITY","currency":"USD","quantity":"tok-secret-123 app-key-abc app-secret-xyz"}]""")]
    public async Task GetPortfolio_UnreadablePositions_ExceptionToString_CarriesNoSecrets(string positions)
    {
        var ex = await Assert.ThrowsAsync<WebullApiException>(
            () => BuildAccessor(PortfolioHandler(acc1PositionsJson: positions)).GetPortfolioAsync(AccessToken));

        var text = ex.ToString();
        Assert.Equal(WebullErrorKind.Unreachable, ex.Kind);
        Assert.DoesNotContain(AccessToken, text);
        Assert.DoesNotContain(AppKey, text);
        Assert.DoesNotContain(AppSecret, text);
    }

    [Fact]
    public async Task GetPortfolio_JsonNumberValues_Parse()
    {
        const string positions = """
            [{"symbol":"VTI","instrument_type":"EQUITY","currency":"USD","quantity":3,"last_price":301.25,"cost_price":280,"unrealized_profit_loss":63.75}]
            """;
        var acc2 = """{"total_asset_currency":"USD","total_cash_balance":"1","total_net_liquidation_value":"2","total_market_value":1.5,"total_day_profit_loss":-0.25,"total_unrealized_profit_loss":0}""";

        var portfolio = await BuildAccessor(PortfolioHandler(acc2, acc1PositionsJson: positions)).GetPortfolioAsync(AccessToken);

        Assert.Equal(new WebullPosition("VTI", "EQUITY", 3m, 301.25m, 280m, 63.75m, "USD"), Assert.Single(portfolio[0].Positions));
        Assert.Equal((1.5m, -0.25m, 0m), (portfolio[1].TotalMarketValue, portfolio[1].TotalDayProfitLoss, portfolio[1].TotalUnrealizedProfitLoss));
    }

    [Theory]
    // All or nothing: the schema marks these fields required, so any of these fails the whole fetch.
    [InlineData("""[{"symbol":"VTI","instrument_type":"EQUITY","currency":"USD","quantity":true}]""")]
    [InlineData("""{"positions":[]}""")]
    [InlineData("""[{"symbol":"VTI","currency":"USD","quantity":"1"}]""")]
    [InlineData("""[{"symbol":null,"instrument_type":"EQUITY","currency":"USD","quantity":"1"}]""")]
    public async Task GetPortfolio_UnexpectedShapes_ThrowUnreachable(string positions)
    {
        var ex = await Assert.ThrowsAsync<WebullApiException>(
            () => BuildAccessor(PortfolioHandler(acc1PositionsJson: positions)).GetPortfolioAsync(AccessToken));

        Assert.Equal(WebullErrorKind.Unreachable, ex.Kind);
    }

    // ── GetOrderPageAsync ───────────────────────────────────────────────────

    private const string OrderHistoryPath = "/trading/orders/historical-orders/list";

    private static readonly WebullOrderWindow OrderWindow =
        new(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc), null);

    // Two combo groups; every number is a string. Webull writes times both with 'Z' and with '+0000'.
    private const string OrderPageJson = """
        {"data":[
          {"combo_type":"NORMAL","client_combo_order_id":"C1","orders":[
            {"order_id":"O1","client_order_id":"c1","symbol":"AAPL","side":"BUY","status":"FILLED","instrument_type":"EQUITY",
             "order_type":"LIMIT","total_quantity":"10","filled_quantity":"10","filled_price":"150.25","limit_price":"151",
             "place_time_at":"2025-03-03T14:30:00.123Z","filled_time_at":"2025-03-03T14:30:01.456+0000"}]},
          {"combo_type":"OTO","orders":[
            {"order_id":"O2","client_order_id":"c2","symbol":"BTCUSD","side":"SELL","status":"PARTIAL_FILLED","instrument_type":"CRYPTO",
             "filled_quantity":"0.01234","filled_price":"60000.5","place_time_at":"2025-06-01T00:00:00.000+0000","filled_time_at":"2025-06-01T00:00:02.000Z"},
            {"order_id":"O3","client_order_id":"c3","symbol":"AAPL","side":"SELL","status":"CANCELLED","instrument_type":"EQUITY",
             "filled_quantity":"","filled_price":"","place_time_at":"2025-06-02T15:00:00.000Z"}]}],
         "pagination_key":"eyJ2IjoxLCJwIjoyfQ=="}
        """;

    private static readonly WebullOrderPage ExpectedOrderPage = new(
        [
            new WebullOrder("ACC1", "O1", "AAPL", "EQUITY", "BUY", "FILLED", 10m, 150.25m,
                new DateTime(2025, 3, 3, 14, 30, 0, 123, DateTimeKind.Utc), new DateTime(2025, 3, 3, 14, 30, 1, 456, DateTimeKind.Utc)),
            new WebullOrder("ACC1", "O2", "BTCUSD", "CRYPTO", "SELL", "PARTIAL_FILLED", 0.01234m, 60000.5m,
                new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 6, 1, 0, 0, 2, DateTimeKind.Utc)),
            new WebullOrder("ACC1", "O3", "AAPL", "EQUITY", "SELL", "CANCELLED", null, null,
                new DateTime(2025, 6, 2, 15, 0, 0, DateTimeKind.Utc), null)
        ],
        "eyJ2IjoxLCJwIjoyfQ==");

    /// <summary>A page holding one order, built from <paramref name="orderFields"/> (JSON members without braces).</summary>
    private static string OnePage(string orderFields) =>
        $$"""{"data":[{"combo_type":"NORMAL","orders":[{{{orderFields}}}]}]}""";

    private const string MinimalOrder =
        """ "order_id":"O9","symbol":"VTI","side":"BUY","status":"FILLED","place_time_at":"2025-02-03T15:00:00.000Z" """;

    private static async Task<WebullOrderPage> ReadPageAsync(string json) =>
        await BuildAccessor(Returning(HttpStatusCode.OK, json)).GetOrderPageAsync(AccessToken, "ACC1", OrderWindow);

    private static async Task<WebullApiException> ReadPageFailsAsync(string json) =>
        await Assert.ThrowsAsync<WebullApiException>(() => ReadPageAsync(json));

    private static void AssertPageEqual(WebullOrderPage expected, WebullOrderPage actual)
    {
        Assert.Equal(expected.Orders, actual.Orders);
        Assert.Equal(expected.NextPaginationKey, actual.NextPaginationKey);
        Assert.All(actual.Orders, o => Assert.Equal(DateTimeKind.Utc, o.PlacedAt.Kind));
    }

    [Fact]
    public async Task GetOrderPage_FlattensEveryGroupsOrders_InAnyStatus_WithTheRequestedAccountId()
    {
        AssertPageEqual(ExpectedOrderPage, await ReadPageAsync(OrderPageJson));
    }

    [Fact]
    public async Task GetOrderPage_FirstPage_SendsAccountAndMillisecondWindow_SignedAndWithToken()
    {
        var handler = Returning(HttpStatusCode.OK, OrderPageJson);

        await BuildAccessor(handler).GetOrderPageAsync(AccessToken, "ACC1", OrderWindow);

        var (request, body) = Assert.Single(handler.Sent);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Null(body);
        Assert.Equal(
            "/trading/orders/historical-orders/list?account_id=ACC1&start_time=2025-01-01T00%3A00%3A00.000Z&end_time=2025-12-31T23%3A59%3A59.999Z",
            request.RequestUri!.PathAndQuery);
        Assert.Equal(AccessToken, Header(request, "x-access-token"));
        Assert.Equal("v3", Header(request, "x-version"));
        var signed = SignedParams(request);
        signed["account_id"] = "ACC1";
        signed["start_time"] = "2025-01-01T00:00:00.000Z";
        signed["end_time"] = "2025-12-31T23:59:59.999Z";
        Assert.Equal(WebullAccessor.ComputeSignature(OrderHistoryPath, signed, null, AppSecret), Header(request, "x-signature"));
    }

    [Fact]
    public async Task GetOrderPage_WithKey_ResendsTheWindow_EncodesTheKeyInTheUri_AndSignsItRaw()
    {
        const string key = "a+b/c=d&e==";
        var handler = Returning(HttpStatusCode.OK, OrderPageJson);

        await BuildAccessor(handler).GetOrderPageAsync(AccessToken, "ACC 1", OrderWindow with { PaginationKey = key });

        var request = Assert.Single(handler.Sent).Request;
        Assert.Equal(
            "/trading/orders/historical-orders/list?account_id=ACC%201&start_time=2025-01-01T00%3A00%3A00.000Z" +
            "&end_time=2025-12-31T23%3A59%3A59.999Z&pagination_key=a%2Bb%2Fc%3Dd%26e%3D%3D",
            request.RequestUri!.PathAndQuery);
        var signed = SignedParams(request);
        signed["account_id"] = "ACC 1";
        signed["start_time"] = "2025-01-01T00:00:00.000Z";
        signed["end_time"] = "2025-12-31T23:59:59.999Z";
        signed["pagination_key"] = key;
        Assert.Equal(WebullAccessor.ComputeSignature(OrderHistoryPath, signed, null, AppSecret), Header(request, "x-signature"));
    }

    [Fact]
    public async Task GetOrderPage_WindowTimes_AreSentToTheMillisecond()
    {
        var handler = Returning(HttpStatusCode.OK, OrderPageJson);
        var window = new WebullOrderWindow(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(9999), new DateTime(2026, 10, 25, 15, 0, 0, 7, DateTimeKind.Utc), null);

        await BuildAccessor(handler).GetOrderPageAsync(AccessToken, "ACC1", window);

        Assert.EndsWith("start_time=2026-01-01T00%3A00%3A00.000Z&end_time=2026-10-25T15%3A00%3A00.007Z", handler.Sent[0].Request.RequestUri!.Query);
    }

    [Fact]
    public async Task GetOrderPage_LaterCallsOnTheSameInstance_WaitAtLeast2Point1SecondsFirst()
    {
        var handler = Returning(HttpStatusCode.OK, OrderPageJson);
        var delays = new List<TimeSpan>();
        var accessor = BuildAccessor(handler, delay: (span, _) =>
        {
            delays.Add(span);
            handler.Log.Add("delay");
            return Task.CompletedTask;
        });

        await accessor.GetOrderPageAsync(AccessToken, "ACC1", OrderWindow);
        await accessor.GetOrderPageAsync(AccessToken, "ACC1", OrderWindow with { PaginationKey = "k" });
        await accessor.GetOrderPageAsync(AccessToken, "ACC2", OrderWindow);

        Assert.Equal(["send", "delay", "send", "delay", "send"], handler.Log.Select(l => l.Split(' ')[0]));
        Assert.All(delays, d => Assert.True(d >= TimeSpan.FromSeconds(2.1)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"data":null}""")]
    [InlineData("""{"data":[]}""")]
    [InlineData("""{"data":[{"combo_type":"NORMAL"}]}""")]
    [InlineData("""{"data":[{"combo_type":"NORMAL","orders":null}]}""")]
    [InlineData("""{"data":[{"combo_type":"NORMAL","orders":[]}],"pagination_key":""}""")]
    [InlineData("""{"data":[],"pagination_key":"  "}""")]
    [InlineData("""{"data":[],"pagination_key":null}""")]
    public async Task GetOrderPage_NoOrdersOrNoKey_EmptyLastPage(string json)
    {
        AssertPageEqual(new WebullOrderPage([], null), await ReadPageAsync(json));
    }

    [Fact]
    public async Task GetOrderPage_MissingOptionalFields_AreNull()
    {
        var page = await ReadPageAsync(OnePage(MinimalOrder));

        Assert.Equal(
            new WebullOrder("ACC1", "O9", "VTI", null, "BUY", "FILLED", null, null, new DateTime(2025, 2, 3, 15, 0, 0, DateTimeKind.Utc), null),
            Assert.Single(page.Orders));
    }

    [Fact]
    public async Task GetOrderPage_NullOrBlankOptionalFields_AreNull()
    {
        var page = await ReadPageAsync(OnePage(MinimalOrder +
            ""","instrument_type":null,"filled_quantity":null,"filled_price":"  ","filled_time_at":"" """));

        var order = Assert.Single(page.Orders);
        Assert.Equal(((string?)null, (decimal?)null, (decimal?)null, (DateTime?)null),
            (order.InstrumentType, order.FilledQuantity, order.FilledPrice, order.FilledAt));
    }

    [Fact]
    public async Task GetOrderPage_UnknownValues_AreKeptAsSent()
    {
        var page = await ReadPageAsync(OnePage(
            """ "order_id":"O9","symbol":"SPY 260116C00600000","side":"SHORT","status":"SOMETHING_NEW","instrument_type":"FUTURES","place_time_at":"2025-02-03T15:00:00.000Z" """));

        var order = Assert.Single(page.Orders);
        Assert.Equal(("SPY 260116C00600000", "SHORT", "SOMETHING_NEW", "FUTURES"), (order.Symbol, order.Side, order.Status, order.InstrumentType));
    }

    [Theory]
    [InlineData(""" "place_time":"1738594800123","filled_time":1738594801000""")]
    [InlineData(""" "place_time":1738594800123,"filled_time":"1738594801000","place_time_at":"","filled_time_at":null""")]
    public async Task GetOrderPage_MissingIsoTimes_FallBackToEpochMilliseconds(string times)
    {
        var page = await ReadPageAsync(OnePage(""" "order_id":"O9","symbol":"VTI","side":"BUY","status":"FILLED",""" + times));

        var order = Assert.Single(page.Orders);
        Assert.Equal(new DateTime(2025, 2, 3, 15, 0, 0, 123, DateTimeKind.Utc), order.PlacedAt);
        Assert.Equal(new DateTime(2025, 2, 3, 15, 0, 1, DateTimeKind.Utc), order.FilledAt);
        Assert.Equal(DateTimeKind.Utc, order.FilledAt!.Value.Kind);
    }

    [Fact]
    public async Task GetOrderPage_IsoTimeWinsOverEpoch()
    {
        var page = await ReadPageAsync(OnePage(MinimalOrder + ""","place_time":"1" """));

        Assert.Equal(new DateTime(2025, 2, 3, 15, 0, 0, DateTimeKind.Utc), Assert.Single(page.Orders).PlacedAt);
    }

    [Theory]
    [InlineData("2025-02-03T10:00:00.000-0500")]
    [InlineData("2025-02-03T15:00:00.000+00:00")]
    [InlineData("2025-02-03T15:00:00Z")]
    [InlineData("2025-02-03T15:00:00.000")] // no offset: read as UTC, never as the server's local time
    public async Task GetOrderPage_PlacementTimes_AreConvertedToUtc(string placed)
    {
        var page = await ReadPageAsync(OnePage(""" "order_id":"O9","symbol":"VTI","side":"BUY","status":"FILLED","place_time_at":""" + $"\"{placed}\""));

        var placedAt = Assert.Single(page.Orders).PlacedAt;
        Assert.Equal(new DateTime(2025, 2, 3, 15, 0, 0, DateTimeKind.Utc), placedAt);
        Assert.Equal(DateTimeKind.Utc, placedAt.Kind);
    }

    [Fact]
    public async Task GetOrderPage_ParsesWithInvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            AssertPageEqual(ExpectedOrderPage, await ReadPageAsync(OrderPageJson));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task GetOrderPage_JsonNumberValues_Parse()
    {
        var page = await ReadPageAsync(OnePage(MinimalOrder + ""","filled_quantity":0.5,"filled_price":12 """));

        Assert.Equal((0.5m, 12m), (page.Orders[0].FilledQuantity!.Value, page.Orders[0].FilledPrice!.Value));
    }

    [Theory]
    [InlineData("order_id")]
    [InlineData("symbol")]
    [InlineData("side")]
    [InlineData("status")]
    [InlineData("place_time_at")]
    public async Task GetOrderPage_MissingRequiredField_ThrowsUnreachable(string field)
    {
        var fields = MinimalOrder.Split(',').Where(f => !f.Trim().StartsWith($"\"{field}\"", StringComparison.Ordinal));

        var ex = await ReadPageFailsAsync(OnePage(string.Join(',', fields)));

        Assert.Equal(WebullErrorKind.Unreachable, ex.Kind);
    }

    [Theory]
    [InlineData(""" "order_id":null,"symbol":"VTI","side":"BUY","status":"FILLED","place_time_at":"2025-02-03T15:00:00Z" """)]
    [InlineData(""" "order_id":"O9","symbol":"","side":"BUY","status":"FILLED","place_time_at":"2025-02-03T15:00:00Z" """)]
    [InlineData(""" "order_id":"O9","symbol":"VTI","side":7,"status":"FILLED","place_time_at":"2025-02-03T15:00:00Z" """)]
    [InlineData(""" "order_id":"O9","symbol":"VTI","side":"BUY","status":"FILLED","place_time_at":"yesterday" """)]
    [InlineData(""" "order_id":"O9","symbol":"VTI","side":"BUY","status":"FILLED","place_time":"soon" """)]
    [InlineData(""" "order_id":"O9","symbol":"VTI","side":"BUY","status":"FILLED","place_time_at":"" """)]
    public async Task GetOrderPage_UnreadableRequiredField_ThrowsUnreachable(string fields)
    {
        Assert.Equal(WebullErrorKind.Unreachable, (await ReadPageFailsAsync(OnePage(fields))).Kind);
    }

    [Theory]
    [InlineData("filled_quantity", "\"abc\"")]
    [InlineData("filled_quantity", "true")]
    [InlineData("filled_price", "\"1.2.3\"")]
    [InlineData("filled_time_at", "\"later\"")]
    [InlineData("filled_time", "\"x\"")]
    [InlineData("filled_time", "\"9999999999999999\"")] // past year 9999
    [InlineData("instrument_type", "5")]
    public async Task GetOrderPage_PresentButUnreadableOptionalField_ThrowsUnreachable(string field, string garbage)
    {
        var ex = await ReadPageFailsAsync(OnePage(MinimalOrder + $",\"{field}\":{garbage}"));

        Assert.Equal(WebullErrorKind.Unreachable, ex.Kind);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{"data":{}}""")]
    [InlineData("""{"data":[{"orders":{}}]}""")]
    [InlineData("""{"data":[],"pagination_key":5}""")]
    public async Task GetOrderPage_UnexpectedShapes_ThrowUnreachable(string json)
    {
        Assert.Equal(WebullErrorKind.Unreachable, (await ReadPageFailsAsync(json)).Kind);
    }

    [Theory]
    [InlineData(417, """{"error_code":"INVALID_TOKEN"}""", WebullErrorKind.TokenRejected)]
    [InlineData(401, """{"error_code":"UNAUTHORIZED"}""", WebullErrorKind.KeyRejected)]
    [InlineData(429, """{"error_code":"TOO_MANY_REQUESTS"}""", WebullErrorKind.Unreachable)]
    [InlineData(417, """{"error_code":"INVALID_PARAMETER"}""", WebullErrorKind.Unreachable)]
    public async Task GetOrderPage_NonSuccess_IsClassified(int status, string json, WebullErrorKind kind)
    {
        var ex = await Assert.ThrowsAsync<WebullApiException>(
            () => BuildAccessor(Returning((HttpStatusCode)status, json)).GetOrderPageAsync(AccessToken, "ACC1", OrderWindow));

        Assert.Equal(kind, ex.Kind);
    }

    [Theory]
    [InlineData("tok-secret-123 app-key-abc app-secret-xyz")]
    [InlineData("""{"data":[{"orders":[{"order_id":"O1","symbol":"A","side":"BUY","status":"FILLED","place_time_at":"2025-01-01T00:00:00Z","filled_quantity":"tok-secret-123 app-key-abc app-secret-xyz"}]}]}""")]
    [InlineData("""{"data":[{"orders":[{"order_id":"O1","symbol":"A","side":"BUY","status":"FILLED","place_time_at":"tok-secret-123 app-key-abc app-secret-xyz"}]}]}""")]
    public async Task GetOrderPage_Unreadable_ExceptionToString_CarriesNoSecrets(string json)
    {
        var ex = await ReadPageFailsAsync(json);

        var text = ex.ToString();
        Assert.Equal(WebullErrorKind.Unreachable, ex.Kind);
        Assert.DoesNotContain(AccessToken, text);
        Assert.DoesNotContain(AppKey, text);
        Assert.DoesNotContain(AppSecret, text);
    }

    [Fact]
    public async Task GetOrderPage_ErrorMessage_CarriesNoSecretsOrKey()
    {
        var handler = Returning(HttpStatusCode.Unauthorized, $$"""{"error_code":"UNAUTHORIZED","message":"echo {{AccessToken}} {{AppKey}}"}""");

        var ex = await Assert.ThrowsAsync<WebullApiException>(
            () => BuildAccessor(handler).GetOrderPageAsync(AccessToken, "ACC1", OrderWindow with { PaginationKey = "secret-cursor" }));

        var text = ex.ToString();
        Assert.DoesNotContain(AccessToken, text);
        Assert.DoesNotContain(AppKey, text);
        Assert.DoesNotContain(AppSecret, text);
        Assert.DoesNotContain("secret-cursor", text);
        Assert.DoesNotContain(Header(handler.Sent[0].Request, "x-signature"), text);
    }

    // ── Error classification ────────────────────────────────────────────────

    [Theory]
    // Observed live against api.sandbox.webull.com (bad App Key; unsupported x-signature-algorithm; unknown route).
    [InlineData(401, """{"error_code":"UNAUTHORIZED","message":"Invalid credentials."}""", WebullErrorKind.KeyRejected, "UNAUTHORIZED")]
    [InlineData(401, """{"error_code":"SIGNATURE_ALGORITHM_NOT_SUPPORTED","message":"Header x-signature-algorithm is invalid."}""", WebullErrorKind.KeyRejected, "SIGNATURE_ALGORITHM_NOT_SUPPORTED")]
    [InlineData(404, """{"error_msg":"404 Route Not Found"}""", WebullErrorKind.Unreachable, null)]
    // Not yet observed live (needs valid sandbox keys).
    [InlineData(403, """{"error_code":"FORBIDDEN"}""", WebullErrorKind.KeyRejected, "FORBIDDEN")]
    [InlineData(417, """{"error_code":"INVALID_TOKEN","message":"token invalid"}""", WebullErrorKind.TokenRejected, "INVALID_TOKEN")]
    [InlineData(401, """{"error_code":"INVALID_TOKEN"}""", WebullErrorKind.TokenRejected, "INVALID_TOKEN")]
    [InlineData(417, """{"error_code":"INVALID_PARAMETER"}""", WebullErrorKind.Unreachable, "INVALID_PARAMETER")]
    [InlineData(400, """{"error_code":"BAD_REQUEST"}""", WebullErrorKind.Unreachable, "BAD_REQUEST")]
    [InlineData(429, """{"error_code":"TOO_MANY_REQUESTS"}""", WebullErrorKind.Unreachable, "TOO_MANY_REQUESTS")]
    [InlineData(500, """{"error_code":"SYSTEM_ERROR"}""", WebullErrorKind.Unreachable, "SYSTEM_ERROR")]
    [InlineData(503, "<html>bad gateway</html>", WebullErrorKind.Unreachable, null)]
    public async Task NonSuccessResponse_IsClassified(int status, string json, WebullErrorKind kind, string? errorCode)
    {
        var handler = Returning((HttpStatusCode)status, json);

        var ex = await Assert.ThrowsAsync<WebullApiException>(() => BuildAccessor(handler).CheckTokenAsync(AccessToken));

        Assert.Equal(kind, ex.Kind);
        Assert.Equal(errorCode, ex.ErrorCode);
        Assert.Equal((HttpStatusCode)status, ex.StatusCode);
    }

    [Fact]
    public async Task NonSuccessResponse_MessageCarriesNoSecrets()
    {
        var handler = Returning(HttpStatusCode.Unauthorized, $$"""{"error_code":"UNAUTHORIZED","message":"echo {{AccessToken}} {{AppKey}}"}""");

        var ex = await Assert.ThrowsAsync<WebullApiException>(() => BuildAccessor(handler).CheckTokenAsync(AccessToken));

        Assert.Contains("401", ex.Message);
        Assert.Contains("UNAUTHORIZED", ex.Message);
        Assert.DoesNotContain(AccessToken, ex.Message);
        Assert.DoesNotContain(AppKey, ex.Message);
        Assert.DoesNotContain(AppSecret, ex.Message);
        Assert.DoesNotContain(Header(handler.Sent[0].Request, "x-signature"), ex.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"token":"t","expires":1,"status":"SOMETHING_NEW"}""")]
    [InlineData("""{"expires":1}""")]
    public async Task SuccessWithUnreadableBody_ThrowsUnreachable(string json)
    {
        var handler = Returning(HttpStatusCode.OK, json);

        var ex = await Assert.ThrowsAsync<WebullApiException>(() => BuildAccessor(handler).CheckTokenAsync(AccessToken));

        Assert.Equal(WebullErrorKind.Unreachable, ex.Kind);
        Assert.Null(ex.ErrorCode);
    }

    [Fact]
    public async Task NetworkError_ThrowsUnreachableWithoutStatus()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("connection refused"));

        var ex = await Assert.ThrowsAsync<WebullApiException>(() => BuildAccessor(handler).CreateTokenAsync());

        Assert.Equal(WebullErrorKind.Unreachable, ex.Kind);
        Assert.Null(ex.StatusCode);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task Timeout_ThrowsUnreachable()
    {
        var handler = new FakeHandler(_ => throw new TaskCanceledException("HttpClient.Timeout elapsed"));

        var ex = await Assert.ThrowsAsync<WebullApiException>(() => BuildAccessor(handler).CreateTokenAsync());

        Assert.Equal(WebullErrorKind.Unreachable, ex.Kind);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesAsCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = new FakeHandler(_ => throw new TaskCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BuildAccessor(handler).CreateTokenAsync(cts.Token));
    }
}
