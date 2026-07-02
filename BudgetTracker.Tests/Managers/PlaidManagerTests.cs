using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;
using BudgetTracker.Server.Managers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class PlaidManagerTests
{
    private readonly Mock<IPlaidAccessor> _plaidAccessor = new(MockBehavior.Strict);
    private readonly Mock<IPlaidItemAccessor> _itemAccessor = new(MockBehavior.Strict);
    private readonly Mock<ITransactionAccessor> _txnAccessor = new(MockBehavior.Strict);
    private readonly Mock<IAccountAccessor> _accountAccessor = new(MockBehavior.Strict);
    private readonly Mock<IPlaidWebhookEngine> _webhookEngine = new(MockBehavior.Strict);
    private readonly PlaidEngine _engine = new();
    private readonly PlaidOptions _options = new();

    private PlaidManager BuildSut() => new(
        _plaidAccessor.Object,
        _itemAccessor.Object,
        _txnAccessor.Object,
        _accountAccessor.Object,
        _engine,
        _webhookEngine.Object,
        Options.Create(_options),
        NullLogger<PlaidManager>.Instance);

    // ── CreateLinkTokenAsync ────────────────────────────────────────────────

    [Fact]
    public async Task CreateLinkToken_DelegatesToAccessorWithCognitoSub()
    {
        var expected = new PlaidLinkTokenResult("link-sandbox-abc", DateTime.UtcNow.AddHours(4));
        _plaidAccessor.Setup(a => a.CreateLinkTokenAsync("cognito-sub-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await BuildSut().CreateLinkTokenAsync(userId: 1, cognitoSub: "cognito-sub-123");

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
        _plaidAccessor.VerifyAll();
    }

    [Fact]
    public async Task CreateLinkToken_AccessorThrows_ReturnsPlainLanguageFailure()
    {
        _plaidAccessor.Setup(a => a.CreateLinkTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        var result = await BuildSut().CreateLinkTokenAsync(userId: 1, cognitoSub: "cognito-sub-123");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.DoesNotContain("network down", result.Error!); // sanitize internal details
    }

    // ── ExchangePublicTokenAsync ────────────────────────────────────────────

    [Fact]
    public async Task Exchange_PersistsItemAndImportsInitialTransactions()
    {
        const int userId = 5;
        _plaidAccessor.Setup(a => a.ExchangePublicTokenAsync("public-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidExchangeResult("access-token-xyz", "plaid-item-1"));
        _plaidAccessor.Setup(a => a.GetItemMetadataAsync("access-token-xyz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidItemMetadata("plaid-item-1", "ins_3", "Chase", null));
        _plaidAccessor.Setup(a => a.GetAccountsAsync("access-token-xyz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlaidAccountDto>
            {
                new("plaid-acct-1", "Plaid Checking", "0000", "depository", "checking")
            });

        _accountAccessor.Setup(a => a.GetByUserIdAsync(userId)).ReturnsAsync(Array.Empty<Account>());
        _accountAccessor.Setup(a => a.CreateAsync(It.IsAny<Account>()))
            .ReturnsAsync(77);

        _itemAccessor.Setup(a => a.ReplaceActiveAsync(
                userId,
                "access-token-xyz",
                "plaid-item-1",
                "ins_3",
                "Chase",
                null,
                It.IsAny<IReadOnlyList<PlaidAccount>>()))
            .ReturnsAsync(11);

        _plaidAccessor.Setup(a => a.SyncTransactionsAsync("access-token-xyz", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidSyncResult(
                Added: [new("plaid-txn-1", "plaid-acct-1", 4.50m, DateTime.Today.AddDays(-1), "Starbucks", "STARBUCKS", false, "FOOD_AND_DRINK")],
                Modified: [],
                RemovedTransactionIds: [],
                NextCursor: "cursor-after"));

        _txnAccessor.Setup(a => a.UpsertImportedAsync(It.IsAny<IEnumerable<Transaction>>()))
            .ReturnsAsync((1, 0));
        _itemAccessor.Setup(a => a.UpdateSyncStateAsync(11, "cursor-after", It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var result = await BuildSut().ExchangePublicTokenAsync(userId, "public-token");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Inserted);
        Assert.Equal(0, result.Value.Updated);
        _itemAccessor.Verify(a => a.ReplaceActiveAsync(
            userId, "access-token-xyz", "plaid-item-1", "ins_3", "Chase", null,
            It.IsAny<IReadOnlyList<PlaidAccount>>()), Times.Once);
        _txnAccessor.Verify(a => a.UpsertImportedAsync(It.IsAny<IEnumerable<Transaction>>()), Times.Once);
    }

    [Fact]
    public async Task Exchange_EmptyPublicToken_ReturnsFailure()
    {
        var result = await BuildSut().ExchangePublicTokenAsync(userId: 1, publicToken: "");

        Assert.False(result.IsSuccess);
        Assert.Equal("Public token is required", result.Error);
    }

    [Fact]
    public async Task Exchange_NullPublicToken_ReturnsFailure()
    {
        var result = await BuildSut().ExchangePublicTokenAsync(userId: 1, publicToken: null!);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Exchange_PlaidAccessorThrows_ReturnsFailureWithoutLeakingDetails()
    {
        _plaidAccessor.Setup(a => a.ExchangePublicTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid API error (status 400): INVALID_PUBLIC_TOKEN — bad token; access_token=secret"));

        var result = await BuildSut().ExchangePublicTokenAsync(userId: 1, publicToken: "public-token");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.DoesNotContain("access_token", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    // ── SyncAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Sync_NoActiveConnection_ReturnsFailure()
    {
        _itemAccessor.Setup(a => a.GetActiveByUserIdAsync(1)).ReturnsAsync((PlaidItem?)null);

        var result = await BuildSut().SyncAsync(userId: 1);

        Assert.False(result.IsSuccess);
        Assert.Equal("No active bank connection to sync", result.Error);
    }

    [Fact]
    public async Task Sync_DelegatesToAccessorAndUpsertsWithDedupe()
    {
        const int userId = 5;
        var plaidItem = new PlaidItem
        {
            Id = 11,
            UserId = userId,
            PlaidItemId = "plaid-item-1",
            SyncCursor = "prev-cursor",
            Accounts = [new PlaidAccount { Id = 1, PlaidAccountId = "plaid-acct-1" }]
        };

        _itemAccessor.Setup(a => a.GetActiveByUserIdAsync(userId)).ReturnsAsync(plaidItem);
        // SyncAsync now funnels through the shared SyncItemAsync, which loads the token by Plaid item_id.
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("plaid-item-1")).ReturnsAsync("access-token-xyz");
        _accountAccessor.Setup(a => a.GetByUserIdAsync(userId)).ReturnsAsync(new List<Account>
        {
            new() { Id = 77, UserId = userId, Name = "Chase - Plaid Checking (••0000)", AccountType = "depository" }
        });

        _plaidAccessor.Setup(a => a.SyncTransactionsAsync("access-token-xyz", "prev-cursor", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidSyncResult(
                Added: [],
                Modified: [],
                RemovedTransactionIds: ["removed-1"],
                NextCursor: "next-cursor"));

        _txnAccessor.Setup(a => a.UpsertImportedAsync(It.IsAny<IEnumerable<Transaction>>()))
            .ReturnsAsync((0, 0));
        _txnAccessor.Setup(a => a.DeleteByPlaidTransactionIdsAsync(It.Is<IEnumerable<string>>(ids => ids.Contains("removed-1"))))
            .ReturnsAsync(1);
        _itemAccessor.Setup(a => a.UpdateSyncStateAsync(11, "next-cursor", It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var result = await BuildSut().SyncAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Removed);
    }

    // ── GetConnectionAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetConnection_ReturnsViewWithInstitutionAndAccounts()
    {
        var plaidItem = new PlaidItem
        {
            Id = 11,
            UserId = 5,
            InstitutionName = "Chase",
            LastSyncedAt = new DateTime(2026, 1, 1),
            Accounts = [new PlaidAccount { PlaidAccountId = "plaid-acct-checking", Name = "Checking", Mask = "0000", AccountType = "depository" }]
        };
        _itemAccessor.Setup(a => a.GetActiveByUserIdAsync(5)).ReturnsAsync(plaidItem);

        var result = await BuildSut().GetConnectionAsync(userId: 5);

        Assert.True(result.IsSuccess);
        Assert.Equal("Chase", result.Value!.InstitutionName);
        Assert.Single(result.Value.Accounts);
        Assert.Equal("plaid-acct-checking", result.Value.Accounts[0].PlaidAccountId);
        Assert.Equal("0000", result.Value.Accounts[0].Mask);
    }

    [Fact]
    public async Task GetConnection_MultipleAccounts_EachRetainsOwnPlaidAccountIdAndMask()
    {
        // BUD-5: the connection projection is the source of the maskByPlaidAccountId map
        // on the ledger. Each linked account must keep its own PlaidAccountId↔Mask pairing
        // (and ordering) so imported rows resolve the correct "•••• {mask}" caption.
        var plaidItem = new PlaidItem
        {
            Id = 11,
            UserId = 5,
            InstitutionName = "Chase",
            LastSyncedAt = new DateTime(2026, 1, 1),
            Accounts =
            [
                new PlaidAccount { PlaidAccountId = "plaid-acct-checking", Name = "Checking", Mask = "0000", AccountType = "depository" },
                new PlaidAccount { PlaidAccountId = "plaid-acct-savings", Name = "Savings", Mask = "1111", AccountType = "depository" },
                new PlaidAccount { PlaidAccountId = "plaid-acct-credit", Name = "Sapphire", Mask = "2222", AccountType = "credit" }
            ]
        };
        _itemAccessor.Setup(a => a.GetActiveByUserIdAsync(5)).ReturnsAsync(plaidItem);

        var result = await BuildSut().GetConnectionAsync(userId: 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Accounts.Count);

        var byId = result.Value.Accounts.ToDictionary(a => a.PlaidAccountId, a => a);
        Assert.Equal("0000", byId["plaid-acct-checking"].Mask);
        Assert.Equal("1111", byId["plaid-acct-savings"].Mask);
        Assert.Equal("2222", byId["plaid-acct-credit"].Mask);
        Assert.Equal("Sapphire", byId["plaid-acct-credit"].Name);
        Assert.Equal("credit", byId["plaid-acct-credit"].AccountType);
    }

    [Fact]
    public async Task GetConnection_NullMask_StillSurfacesPlaidAccountId()
    {
        // BUD-5 edge case: some Plaid accounts have no mask. The PlaidAccountId must still
        // come through (so the row is keyed correctly); only the mask caption is omitted.
        var plaidItem = new PlaidItem
        {
            Id = 11,
            UserId = 5,
            InstitutionName = "Chase",
            Accounts = [new PlaidAccount { PlaidAccountId = "plaid-acct-no-mask", Name = "Brokerage", Mask = null, AccountType = "investment" }]
        };
        _itemAccessor.Setup(a => a.GetActiveByUserIdAsync(5)).ReturnsAsync(plaidItem);

        var result = await BuildSut().GetConnectionAsync(userId: 5);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Accounts);
        Assert.Equal("plaid-acct-no-mask", result.Value.Accounts[0].PlaidAccountId);
        Assert.Null(result.Value.Accounts[0].Mask);
    }

    [Fact]
    public async Task GetConnection_NoAccounts_ReturnsEmptyAccountsList()
    {
        // BUD-5 edge case: a connection with zero linked accounts must yield an empty
        // (non-null) list so the client's mask map builds without throwing.
        var plaidItem = new PlaidItem
        {
            Id = 11,
            UserId = 5,
            InstitutionName = "Chase",
            Accounts = []
        };
        _itemAccessor.Setup(a => a.GetActiveByUserIdAsync(5)).ReturnsAsync(plaidItem);

        var result = await BuildSut().GetConnectionAsync(userId: 5);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.Accounts);
        Assert.Empty(result.Value.Accounts);
    }

    [Fact]
    public async Task GetConnection_NoActive_ReturnsFailure()
    {
        _itemAccessor.Setup(a => a.GetActiveByUserIdAsync(5)).ReturnsAsync((PlaidItem?)null);

        var result = await BuildSut().GetConnectionAsync(userId: 5);

        Assert.False(result.IsSuccess);
    }

    // ── DisconnectAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Disconnect_RevokesPlaidItemAndDeactivates()
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(5)).ReturnsAsync("access-token-xyz");
        _plaidAccessor.Setup(a => a.RemoveItemAsync("access-token-xyz", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _itemAccessor.Setup(a => a.DeactivateActiveAsync(5)).ReturnsAsync(true);

        var result = await BuildSut().DisconnectAsync(userId: 5);

        Assert.True(result.IsSuccess);
        _plaidAccessor.Verify(a => a.RemoveItemAsync("access-token-xyz", It.IsAny<CancellationToken>()), Times.Once);
        _itemAccessor.Verify(a => a.DeactivateActiveAsync(5), Times.Once);
    }

    [Fact]
    public async Task Disconnect_NoActiveConnection_ReturnsFailure()
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(5)).ReturnsAsync((string?)null);

        var result = await BuildSut().DisconnectAsync(userId: 5);

        Assert.False(result.IsSuccess);
        Assert.Equal("No active bank connection to disconnect", result.Error);
    }

    [Fact]
    public async Task Disconnect_PlaidRevokeFails_StillDeactivatesLocally()
    {
        // Plaid may be down or the item already revoked. We still soft-delete locally to free the user.
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(5)).ReturnsAsync("access-token-xyz");
        _plaidAccessor.Setup(a => a.RemoveItemAsync("access-token-xyz", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid down"));
        _itemAccessor.Setup(a => a.DeactivateActiveAsync(5)).ReturnsAsync(true);

        var result = await BuildSut().DisconnectAsync(userId: 5);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.DeactivateActiveAsync(5), Times.Once);
    }

    // ── HandleTransactionsWebhookAsync ──────────────────────────────────────

    private const string ValidJwt = "header.payload.signature";
    private static readonly PlaidJwkDto TestJwk = new("EC", "P-256", "x", "y", "kid-1", "sig", "ES256");

    /// <summary>Arranges a fully-verified webhook: kid extracted, JWK fetched, signature valid.</summary>
    private void ArrangeVerifiedWebhook(string rawBody)
    {
        _webhookEngine.Setup(e => e.ExtractKeyId(ValidJwt)).Returns("kid-1");
        _plaidAccessor.Setup(a => a.GetWebhookVerificationKeyAsync("kid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestJwk);
        _webhookEngine.Setup(e => e.VerifyWebhook(ValidJwt, TestJwk, rawBody)).Returns(true);
    }

    /// <summary>Sets up all accessor calls a verified sync of <paramref name="plaidItemId"/> will make.</summary>
    private void ArrangeSyncForItem(string plaidItemId, PlaidItem item)
    {
        _itemAccessor.Setup(a => a.GetByPlaidItemIdAsync(plaidItemId)).ReturnsAsync(item);
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync(plaidItemId)).ReturnsAsync("access-token-xyz");
        _accountAccessor.Setup(a => a.GetByUserIdAsync(item.UserId)).ReturnsAsync(new List<Account>
        {
            new() { Id = 77, UserId = item.UserId, Name = "Chase - Plaid Checking (••0000)", AccountType = "depository" }
        });
        _plaidAccessor.Setup(a => a.SyncTransactionsAsync("access-token-xyz", item.SyncCursor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidSyncResult(
                Added: [new("plaid-txn-1", "plaid-acct-1", 4.50m, DateTime.Today, "Starbucks", "STARBUCKS", false, "FOOD_AND_DRINK")],
                Modified: [],
                RemovedTransactionIds: [],
                NextCursor: "next-cursor"));
        _txnAccessor.Setup(a => a.UpsertImportedAsync(It.IsAny<IEnumerable<Transaction>>())).ReturnsAsync((1, 0));
        _itemAccessor.Setup(a => a.UpdateSyncStateAsync(item.Id, "next-cursor", It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
    }

    private static PlaidItem ItemFor(string plaidItemId) => new()
    {
        Id = 11,
        UserId = 5,
        PlaidItemId = plaidItemId,
        SyncCursor = "prev-cursor",
        Accounts = [new PlaidAccount { Id = 1, PlaidAccountId = "plaid-acct-1" }]
    };

    [Fact]
    public async Task Webhook_ValidTransactionsSyncUpdatesAvailable_LooksUpItemAndSyncs()
    {
        var body = """{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE","item_id":"plaid-item-1"}""";
        ArrangeVerifiedWebhook(body);
        ArrangeSyncForItem("plaid-item-1", ItemFor("plaid-item-1"));

        var result = await BuildSut().HandleTransactionsWebhookAsync(ValidJwt, body);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.GetByPlaidItemIdAsync("plaid-item-1"), Times.Once);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("access-token-xyz", "prev-cursor", It.IsAny<CancellationToken>()), Times.Once);
        _itemAccessor.Verify(a => a.UpdateSyncStateAsync(11, "next-cursor", It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_InvalidVerification_NoSyncReturnsSilentSuccess()
    {
        var body = """{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE","item_id":"plaid-item-1"}""";
        _webhookEngine.Setup(e => e.ExtractKeyId(ValidJwt)).Returns("kid-1");
        _plaidAccessor.Setup(a => a.GetWebhookVerificationKeyAsync("kid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestJwk);
        _webhookEngine.Setup(e => e.VerifyWebhook(ValidJwt, TestJwk, body)).Returns(false);

        var result = await BuildSut().HandleTransactionsWebhookAsync(ValidJwt, body);

        // Silent reject: neutral success, but NO item lookup and NO sync.
        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.GetByPlaidItemIdAsync(It.IsAny<string>()), Times.Never);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Webhook_UnparseableKid_ReturnsSilentSuccessWithoutFetchingKey()
    {
        var body = """{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE","item_id":"plaid-item-1"}""";
        _webhookEngine.Setup(e => e.ExtractKeyId(ValidJwt)).Returns((string?)null);

        var result = await BuildSut().HandleTransactionsWebhookAsync(ValidJwt, body);

        Assert.True(result.IsSuccess);
        _plaidAccessor.Verify(a => a.GetWebhookVerificationKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _itemAccessor.Verify(a => a.GetByPlaidItemIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Webhook_NonTransactionsType_IgnoredNoSync()
    {
        var body = """{"webhook_type":"ITEM","webhook_code":"ERROR","item_id":"plaid-item-1"}""";
        ArrangeVerifiedWebhook(body);

        var result = await BuildSut().HandleTransactionsWebhookAsync(ValidJwt, body);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.GetByPlaidItemIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Webhook_UnknownTransactionsCode_IgnoredNoSync()
    {
        var body = """{"webhook_type":"TRANSACTIONS","webhook_code":"RECURRING_TRANSACTIONS_UPDATE","item_id":"plaid-item-1"}""";
        ArrangeVerifiedWebhook(body);

        var result = await BuildSut().HandleTransactionsWebhookAsync(ValidJwt, body);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.GetByPlaidItemIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Webhook_UnknownItemId_NoOpSuccess()
    {
        var body = """{"webhook_type":"TRANSACTIONS","webhook_code":"DEFAULT_UPDATE","item_id":"ghost-item"}""";
        ArrangeVerifiedWebhook(body);
        _itemAccessor.Setup(a => a.GetByPlaidItemIdAsync("ghost-item")).ReturnsAsync((PlaidItem?)null);

        var result = await BuildSut().HandleTransactionsWebhookAsync(ValidJwt, body);

        Assert.True(result.IsSuccess);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── SweepAllAsync ───────────────────────────────────────────────────────

    private void ArrangeSweepSyncForItem(PlaidItem item)
    {
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync(item.PlaidItemId)).ReturnsAsync($"token-{item.Id}");
        _accountAccessor.Setup(a => a.GetByUserIdAsync(item.UserId)).ReturnsAsync(new List<Account>());
        _plaidAccessor.Setup(a => a.SyncTransactionsAsync($"token-{item.Id}", item.SyncCursor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidSyncResult([], [], [], "next"));
        _txnAccessor.Setup(a => a.UpsertImportedAsync(It.IsAny<IEnumerable<Transaction>>())).ReturnsAsync((0, 0));
        _itemAccessor.Setup(a => a.UpdateSyncStateAsync(item.Id, "next", It.IsAny<DateTime>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Sweep_SyncsEveryActiveItem()
    {
        var itemA = new PlaidItem { Id = 1, UserId = 10, PlaidItemId = "item-a", SyncCursor = "c-a", Accounts = [] };
        var itemB = new PlaidItem { Id = 2, UserId = 20, PlaidItemId = "item-b", SyncCursor = "c-b", Accounts = [] };
        _itemAccessor.Setup(a => a.GetAllActiveAsync()).ReturnsAsync([itemA, itemB]);
        ArrangeSweepSyncForItem(itemA);
        ArrangeSweepSyncForItem(itemB);

        var result = await BuildSut().SweepAllAsync();

        Assert.True(result.IsSuccess);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("token-1", "c-a", It.IsAny<CancellationToken>()), Times.Once);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("token-2", "c-b", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sweep_OneItemThrows_OthersStillSync()
    {
        var bad = new PlaidItem { Id = 1, UserId = 10, PlaidItemId = "item-bad", SyncCursor = "c-bad", Accounts = [] };
        var good = new PlaidItem { Id = 2, UserId = 20, PlaidItemId = "item-good", SyncCursor = "c-good", Accounts = [] };
        _itemAccessor.Setup(a => a.GetAllActiveAsync()).ReturnsAsync([bad, good]);

        // Bad item blows up during token fetch.
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("item-bad")).ThrowsAsync(new Exception("boom"));
        ArrangeSweepSyncForItem(good);

        var result = await BuildSut().SweepAllAsync();

        Assert.True(result.IsSuccess);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("token-2", "c-good", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sweep_WebhookConfigured_RegistersWebhookOnActiveItems()
    {
        _options.WebhookUrl = "https://tunnel.example/api/plaid/webhook";
        var item = new PlaidItem { Id = 1, UserId = 10, PlaidItemId = "item-a", SyncCursor = "c-a", Accounts = [] };
        _itemAccessor.Setup(a => a.GetAllActiveAsync()).ReturnsAsync([item]);
        ArrangeSweepSyncForItem(item);
        _plaidAccessor.Setup(a => a.UpdateWebhookAsync("token-1", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().SweepAllAsync();

        Assert.True(result.IsSuccess);
        _plaidAccessor.Verify(a => a.UpdateWebhookAsync("token-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sweep_WebhookNotConfigured_DoesNotRegisterWebhook()
    {
        // Default _options.WebhookUrl is empty — no UpdateWebhookAsync call should occur.
        var item = new PlaidItem { Id = 1, UserId = 10, PlaidItemId = "item-a", SyncCursor = "c-a", Accounts = [] };
        _itemAccessor.Setup(a => a.GetAllActiveAsync()).ReturnsAsync([item]);
        ArrangeSweepSyncForItem(item);

        var result = await BuildSut().SweepAllAsync();

        Assert.True(result.IsSuccess);
        _plaidAccessor.Verify(a => a.UpdateWebhookAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Webhook — additional coverage (johnny review) ───────────────────────

    [Fact]
    public async Task Webhook_JwkFetchThrows_ReturnsSilentSuccessNoSync()
    {
        var body = """{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE","item_id":"plaid-item-1"}""";
        _webhookEngine.Setup(e => e.ExtractKeyId(ValidJwt)).Returns("kid-1");
        _plaidAccessor.Setup(a => a.GetWebhookVerificationKeyAsync("kid-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid key endpoint down"));

        var result = await BuildSut().HandleTransactionsWebhookAsync(ValidJwt, body);

        // A transient JWK-fetch failure must not throw and must not verify/sync — neutral success.
        Assert.True(result.IsSuccess);
        _webhookEngine.Verify(e => e.VerifyWebhook(It.IsAny<string>(), It.IsAny<PlaidJwkDto>(), It.IsAny<string>()), Times.Never);
        _itemAccessor.Verify(a => a.GetByPlaidItemIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("SYNC_UPDATES_AVAILABLE")]
    [InlineData("DEFAULT_UPDATE")]
    [InlineData("HISTORICAL_UPDATE")]
    [InlineData("INITIAL_UPDATE")]
    public async Task Webhook_EachActionableCode_TriggersSync(string webhookCode)
    {
        var body = $$"""{"webhook_type":"TRANSACTIONS","webhook_code":"{{webhookCode}}","item_id":"plaid-item-1"}""";
        ArrangeVerifiedWebhook(body);
        ArrangeSyncForItem("plaid-item-1", ItemFor("plaid-item-1"));

        var result = await BuildSut().HandleTransactionsWebhookAsync(ValidJwt, body);

        Assert.True(result.IsSuccess);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("access-token-xyz", "prev-cursor", It.IsAny<CancellationToken>()), Times.Once);
        _itemAccessor.Verify(a => a.UpdateSyncStateAsync(11, "next-cursor", It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_VerifiedButDownstreamSyncThrows_ReturnsFailureWithoutThrowing()
    {
        var body = """{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE","item_id":"plaid-item-1"}""";
        ArrangeVerifiedWebhook(body);
        _itemAccessor.Setup(a => a.GetByPlaidItemIdAsync("plaid-item-1")).ReturnsAsync(ItemFor("plaid-item-1"));
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("plaid-item-1")).ReturnsAsync("access-token-xyz");
        _accountAccessor.Setup(a => a.GetByUserIdAsync(5)).ReturnsAsync(new List<Account>());
        // The Plaid sync call fails — the handler surfaces a failure Result but never throws.
        _plaidAccessor.Setup(a => a.SyncTransactionsAsync("access-token-xyz", "prev-cursor", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid sync down"));

        var result = await BuildSut().HandleTransactionsWebhookAsync(ValidJwt, body);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Webhook_RealEngine_ValidSignedJwt_FlowsThroughToSync()
    {
        // Integration: REAL PlaidWebhookEngine (not the mock). Proves the Manager passes the raw body
        // verbatim to the engine and that a genuinely-valid ES256 token drives a sync end-to-end.
        using var key = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var body = """{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE","item_id":"plaid-item-1"}""";
        var jwt = SignValidJwt(key, body, keyId: "real-kid");
        var jwk = JwkFor(key, "real-kid");

        _plaidAccessor.Setup(a => a.GetWebhookVerificationKeyAsync("real-kid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(jwk);
        ArrangeSyncForItem("plaid-item-1", ItemFor("plaid-item-1"));

        var sut = BuildSutWithRealWebhookEngine();
        var result = await sut.HandleTransactionsWebhookAsync(jwt, body);

        Assert.True(result.IsSuccess);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("access-token-xyz", "prev-cursor", It.IsAny<CancellationToken>()), Times.Once);
        _itemAccessor.Verify(a => a.UpdateSyncStateAsync(11, "next-cursor", It.IsAny<DateTime>()), Times.Once);
    }

    // ── Sweep — additional coverage (johnny review) ─────────────────────────

    [Fact]
    public async Task Sweep_NoActiveItems_SucceedsWithNoDownstreamCalls()
    {
        _itemAccessor.Setup(a => a.GetAllActiveAsync()).ReturnsAsync([]);

        var result = await BuildSut().SweepAllAsync();

        Assert.True(result.IsSuccess);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _itemAccessor.Verify(a => a.GetAccessTokenByPlaidItemIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Sweep_AdvancesCursorForEachItem()
    {
        var itemA = new PlaidItem { Id = 1, UserId = 10, PlaidItemId = "item-a", SyncCursor = "c-a", Accounts = [] };
        var itemB = new PlaidItem { Id = 2, UserId = 20, PlaidItemId = "item-b", SyncCursor = "c-b", Accounts = [] };
        _itemAccessor.Setup(a => a.GetAllActiveAsync()).ReturnsAsync([itemA, itemB]);
        ArrangeSweepSyncForItem(itemA);
        ArrangeSweepSyncForItem(itemB);

        var result = await BuildSut().SweepAllAsync();

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.UpdateSyncStateAsync(1, "next", It.IsAny<DateTime>()), Times.Once);
        _itemAccessor.Verify(a => a.UpdateSyncStateAsync(2, "next", It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Sweep_UpdateWebhookThrows_ItemStillSyncsAndOthersUnaffected()
    {
        // Pins fix #2: webhook registration is best-effort in its own try/catch, so a failed
        // /item/webhook/update never skips that item's sync — and a second good item still syncs.
        _options.WebhookUrl = "https://tunnel.example/api/plaid/webhook";
        var flaky = new PlaidItem { Id = 1, UserId = 10, PlaidItemId = "item-flaky", SyncCursor = "c-flaky", Accounts = [] };
        var good = new PlaidItem { Id = 2, UserId = 20, PlaidItemId = "item-good", SyncCursor = "c-good", Accounts = [] };
        _itemAccessor.Setup(a => a.GetAllActiveAsync()).ReturnsAsync([flaky, good]);
        ArrangeSweepSyncForItem(flaky);
        ArrangeSweepSyncForItem(good);

        // Registration fails only for the flaky item; succeeds for the good one.
        _plaidAccessor.Setup(a => a.UpdateWebhookAsync("token-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("webhook update failed"));
        _plaidAccessor.Setup(a => a.UpdateWebhookAsync("token-2", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await BuildSut().SweepAllAsync();

        Assert.True(result.IsSuccess);
        // The flaky item still synced despite its registration throwing.
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("token-1", "c-flaky", It.IsAny<CancellationToken>()), Times.Once);
        _itemAccessor.Verify(a => a.UpdateSyncStateAsync(1, "next", It.IsAny<DateTime>()), Times.Once);
        // The good item is unaffected.
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("token-2", "c-good", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Real-engine harness helpers ─────────────────────────────────────────

    private PlaidManager BuildSutWithRealWebhookEngine() => new(
        _plaidAccessor.Object,
        _itemAccessor.Object,
        _txnAccessor.Object,
        _accountAccessor.Object,
        _engine,
        new PlaidWebhookEngine(),
        Options.Create(_options),
        NullLogger<PlaidManager>.Instance);

    private static string SignValidJwt(System.Security.Cryptography.ECDsa key, string body, string keyId)
    {
        var headerJson = $$"""{"alg":"ES256","kid":"{{keyId}}","typ":"JWT"}""";
        var iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bodyHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var payloadJson = $$"""{"iat":{{iat}},"request_body_sha256":"{{bodyHash}}"}""";

        var signingInput = $"{B64Url(System.Text.Encoding.UTF8.GetBytes(headerJson))}.{B64Url(System.Text.Encoding.UTF8.GetBytes(payloadJson))}";
        var signature = key.SignData(System.Text.Encoding.ASCII.GetBytes(signingInput),
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{signingInput}.{B64Url(signature)}";
    }

    private static PlaidJwkDto JwkFor(System.Security.Cryptography.ECDsa key, string keyId)
    {
        var p = key.ExportParameters(includePrivateParameters: false);
        return new PlaidJwkDto("EC", "P-256", B64Url(p.Q.X!), B64Url(p.Q.Y!), keyId, "sig", "ES256");
    }

    private static string B64Url(byte[] bytes) => Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(bytes);
}
