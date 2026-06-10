using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class PlaidManagerTests
{
    private readonly Mock<IPlaidAccessor> _plaidAccessor = new(MockBehavior.Strict);
    private readonly Mock<IPlaidItemAccessor> _itemAccessor = new(MockBehavior.Strict);
    private readonly Mock<ITransactionAccessor> _txnAccessor = new(MockBehavior.Strict);
    private readonly Mock<IAccountAccessor> _accountAccessor = new(MockBehavior.Strict);
    private readonly PlaidEngine _engine = new();

    private PlaidManager BuildSut() => new(
        _plaidAccessor.Object,
        _itemAccessor.Object,
        _txnAccessor.Object,
        _accountAccessor.Object,
        _engine);

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
            SyncCursor = "prev-cursor",
            Accounts = [new PlaidAccount { Id = 1, PlaidAccountId = "plaid-acct-1" }]
        };

        _itemAccessor.Setup(a => a.GetActiveByUserIdAsync(userId)).ReturnsAsync(plaidItem);
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(userId)).ReturnsAsync("access-token-xyz");
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
            Accounts = [new PlaidAccount { Name = "Checking", Mask = "0000", AccountType = "depository" }]
        };
        _itemAccessor.Setup(a => a.GetActiveByUserIdAsync(5)).ReturnsAsync(plaidItem);

        var result = await BuildSut().GetConnectionAsync(userId: 5);

        Assert.True(result.IsSuccess);
        Assert.Equal("Chase", result.Value!.InstitutionName);
        Assert.Single(result.Value.Accounts);
        Assert.Equal("0000", result.Value.Accounts[0].Mask);
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
}
