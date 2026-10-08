using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;
using BudgetTracker.Server.Managers;
using Microsoft.Extensions.Logging;
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
    private readonly Mock<ICategoryAccessor> _categoryAccessor = new(MockBehavior.Strict);
    private readonly Mock<IPlaidWebhookEngine> _webhookEngine = new(MockBehavior.Strict);
    private readonly PlaidEngine _engine = new();
    private readonly PlaidOptions _options = new();
    private readonly Mock<ILogger<PlaidManager>> _logger = new();

    public PlaidManagerTests()
    {
        // Sync resolves Plaid's suggested category against the user's categories (BUD-9).
        // Default to "no mappings configured"; tests that care override this.
        _categoryAccessor
            .Setup(a => a.GetByUserIdAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<Category>());
    }

    private PlaidManager BuildSut() => new(
        _plaidAccessor.Object,
        _itemAccessor.Object,
        _txnAccessor.Object,
        _accountAccessor.Object,
        _categoryAccessor.Object,
        _engine,
        _webhookEngine.Object,
        Options.Create(_options),
        _logger.Object);

    private void VerifyWarningLogged(Times times) => _logger.Verify(l => l.Log(
        LogLevel.Warning,
        It.IsAny<EventId>(),
        It.IsAny<It.IsAnyType>(),
        It.IsAny<Exception?>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

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

        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(userId)).ReturnsAsync([]);
        _accountAccessor.Setup(a => a.GetByUserIdAsync(userId)).ReturnsAsync(Array.Empty<Account>());
        _accountAccessor.Setup(a => a.CreateAsync(It.IsAny<Account>()))
            .ReturnsAsync(77);

        _itemAccessor.Setup(a => a.AddAsync(
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
        _itemAccessor.Verify(a => a.AddAsync(
            userId, "access-token-xyz", "plaid-item-1", "ins_3", "Chase", null,
            It.IsAny<IReadOnlyList<PlaidAccount>>()), Times.Once);
        _txnAccessor.Verify(a => a.UpsertImportedAsync(It.IsAny<IEnumerable<Transaction>>()), Times.Once);
    }

    [Fact]
    public async Task Exchange_UserAlreadyHasActiveItems_AddsNewItemWithoutDeactivatingOrRevokingExisting()
    {
        // Multi-bank: linking a second institution must leave the first one untouched.
        // Strict mocks mean any DeactivateAsync / RemoveItemAsync call would throw and fail the test.
        const int userId = 5;
        _plaidAccessor.Setup(a => a.ExchangePublicTokenAsync("public-token-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidExchangeResult("access-token-2", "plaid-item-2"));
        _plaidAccessor.Setup(a => a.GetItemMetadataAsync("access-token-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidItemMetadata("plaid-item-2", "ins_4", "Wells Fargo", null));
        _plaidAccessor.Setup(a => a.GetAccountsAsync("access-token-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlaidAccountDto> { new("plaid-acct-wf", "Everyday Checking", "9999", "depository", "checking") });

        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(userId)).ReturnsAsync([ChaseItem()]);
        _accountAccessor.Setup(a => a.GetByUserIdAsync(userId)).ReturnsAsync(new List<Account>
        {
            new() { Id = 77, UserId = userId, Name = "Chase - Plaid Checking (••0000)", AccountType = "depository" }
        });
        _accountAccessor.Setup(a => a.CreateAsync(It.IsAny<Account>())).ReturnsAsync(78);

        _itemAccessor.Setup(a => a.AddAsync(
                userId, "access-token-2", "plaid-item-2", "ins_4", "Wells Fargo", null,
                It.IsAny<IReadOnlyList<PlaidAccount>>()))
            .ReturnsAsync(12);
        _plaidAccessor.Setup(a => a.SyncTransactionsAsync("access-token-2", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidSyncResult([], [], [], "cursor-2"));
        _txnAccessor.Setup(a => a.UpsertImportedAsync(It.IsAny<IEnumerable<Transaction>>())).ReturnsAsync((0, 0));
        _itemAccessor.Setup(a => a.UpdateSyncStateAsync(12, "cursor-2", It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        var result = await BuildSut().ExchangePublicTokenAsync(userId, "public-token-2");

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.AddAsync(
            userId, "access-token-2", "plaid-item-2", "ins_4", "Wells Fargo", null,
            It.IsAny<IReadOnlyList<PlaidAccount>>()), Times.Once);
        _itemAccessor.Verify(a => a.DeactivateAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _plaidAccessor.Verify(a => a.RemoveItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An active Chase item whose only account is "Plaid Checking" ••0000 (account id plaid-acct-1).</summary>
    private static PlaidItem ChaseItem() => new()
    {
        Id = 11,
        UserId = 5,
        PlaidItemId = "plaid-item-1",
        InstitutionId = "ins_3",
        InstitutionName = "Chase",
        IsActive = true,
        Accounts = [new PlaidAccount { PlaidAccountId = "plaid-acct-1", Name = "Plaid Checking", Mask = "0000", AccountType = "depository" }]
    };

    /// <summary>Arranges a Plaid exchange that returns a second Chase item holding the same account.</summary>
    private void ArrangeDuplicateChaseExchange()
    {
        _plaidAccessor.Setup(a => a.ExchangePublicTokenAsync("public-token-dup", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidExchangeResult("access-token-dup", "plaid-item-dup"));
        _plaidAccessor.Setup(a => a.GetItemMetadataAsync("access-token-dup", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidItemMetadata("plaid-item-dup", "ins_3", "Chase", null));
        _plaidAccessor.Setup(a => a.GetAccountsAsync("access-token-dup", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlaidAccountDto> { new("plaid-acct-1", "Plaid Checking", "0000", "depository", "checking") });
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([ChaseItem()]);
    }

    [Fact]
    public async Task Should_RejectWithAlreadyLinkedMessageBeforePersisting_When_InstitutionIsAlreadyActive()
    {
        ArrangeDuplicateChaseExchange();
        _plaidAccessor.Setup(a => a.RemoveItemAsync("access-token-dup", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().ExchangePublicTokenAsync(userId: 5, "public-token-dup");

        Assert.False(result.IsSuccess);
        Assert.Equal("This institution is already linked. Disconnect it first if you want to link it again.", result.Error);
        // Strict mocks: no account creation or sync can have happened either.
        _itemAccessor.Verify(a => a.AddAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<IReadOnlyList<PlaidAccount>>()), Times.Never);
        _itemAccessor.Verify(a => a.DeactivateAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Should_RevokeTheDuplicatePlaidItem_When_InstitutionIsAlreadyActive()
    {
        // The exchange already created a billable Item at Plaid; /item/remove on the NEW token stops that billing.
        ArrangeDuplicateChaseExchange();
        _plaidAccessor.Setup(a => a.RemoveItemAsync("access-token-dup", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await BuildSut().ExchangePublicTokenAsync(userId: 5, "public-token-dup");

        _plaidAccessor.Verify(a => a.RemoveItemAsync("access-token-dup", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Should_StillReturnAlreadyLinkedAndLogWarning_When_RevokingTheDuplicateFails()
    {
        ArrangeDuplicateChaseExchange();
        _plaidAccessor.Setup(a => a.RemoveItemAsync("access-token-dup", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid down"));

        var result = await BuildSut().ExchangePublicTokenAsync(userId: 5, "public-token-dup");

        Assert.False(result.IsSuccess);
        Assert.Equal("This institution is already linked. Disconnect it first if you want to link it again.", result.Error);
        VerifyWarningLogged(Times.Once());
    }

    /// <summary>Arranges an exchange that reaches the persist step, where <see cref="IPlaidItemAccessor.AddAsync"/> throws.</summary>
    private void ArrangeExchangeWhosePersistFails()
    {
        _plaidAccessor.Setup(a => a.ExchangePublicTokenAsync("public-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidExchangeResult("access-token-xyz", "plaid-item-1"));
        _plaidAccessor.Setup(a => a.GetItemMetadataAsync("access-token-xyz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidItemMetadata("plaid-item-1", "ins_3", "Chase", null));
        _plaidAccessor.Setup(a => a.GetAccountsAsync("access-token-xyz", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlaidAccountDto>());
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([]);
        _accountAccessor.Setup(a => a.GetByUserIdAsync(5)).ReturnsAsync(Array.Empty<Account>());
        _itemAccessor.Setup(a => a.AddAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<IReadOnlyList<PlaidAccount>>()))
            .ThrowsAsync(new InvalidOperationException("unique violation"));
    }

    [Fact]
    public async Task Exchange_PersistFails_ReturnsFailure()
    {
        ArrangeExchangeWhosePersistFails();
        _plaidAccessor.Setup(a => a.RemoveItemAsync("access-token-xyz", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().ExchangePublicTokenAsync(userId: 5, "public-token");

        Assert.False(result.IsSuccess);
        Assert.Equal("Could not save the new bank connection. Please try again.", result.Error);
    }

    [Fact]
    public async Task Exchange_PersistFails_RevokesExchangedPlaidItem()
    {
        // The exchange already created a billable Item at Plaid; with no row to reference it, nothing else ever would.
        ArrangeExchangeWhosePersistFails();
        _plaidAccessor.Setup(a => a.RemoveItemAsync("access-token-xyz", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await BuildSut().ExchangePublicTokenAsync(userId: 5, "public-token");

        _plaidAccessor.Verify(a => a.RemoveItemAsync("access-token-xyz", It.IsAny<CancellationToken>()), Times.Once);
        VerifyWarningLogged(Times.Once());
    }

    [Fact]
    public async Task Exchange_PersistFailsAndRevokeFails_StillReturnsSaveFailureAndLogsBoth()
    {
        ArrangeExchangeWhosePersistFails();
        _plaidAccessor.Setup(a => a.RemoveItemAsync("access-token-xyz", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid down"));

        var result = await BuildSut().ExchangePublicTokenAsync(userId: 5, "public-token");

        Assert.False(result.IsSuccess);
        Assert.Equal("Could not save the new bank connection. Please try again.", result.Error);
        VerifyWarningLogged(Times.Exactly(2));
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
    public async Task Sync_NoActiveConnections_ReturnsZeroSummary()
    {
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(1)).ReturnsAsync([]);
        var before = DateTime.UtcNow;

        var result = await BuildSut().SyncAsync(userId: 1);

        // The dashboard calls this on open; having no banks linked is not an error.
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.Inserted);
        Assert.Equal(0, result.Value.Updated);
        Assert.Equal(0, result.Value.Removed);
        Assert.InRange(result.Value.SyncedAt, before, DateTime.UtcNow);
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

        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(userId)).ReturnsAsync([plaidItem]);
        // SyncAsync now funnels through the shared SyncItemAsync, which loads the token by Plaid item_id.
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("plaid-item-1")).ReturnsAsync(AccessTokenLookup.Found("access-token-xyz"));
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

    [Fact]
    public async Task Sync_AppliesMappedCategoryToImportedTransactions()
    {
        const int userId = 5;
        var plaidItem = new PlaidItem
        {
            Id = 11,
            UserId = userId,
            PlaidItemId = "plaid-item-1",
            SyncCursor = "prev-cursor",
            Accounts = [new PlaidAccount { Id = 1, PlaidAccountId = "plaid-acct-1", Name = "Plaid Checking", Mask = "0000" }]
        };

        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(userId)).ReturnsAsync([plaidItem]);
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("plaid-item-1")).ReturnsAsync(AccessTokenLookup.Found("access-token-xyz"));
        _accountAccessor.Setup(a => a.GetByUserIdAsync(userId)).ReturnsAsync(new List<Account>
        {
            new() { Id = 77, UserId = userId, Name = "Chase - Plaid Checking (••0000)", AccountType = "depository" }
        });

        // The user has mapped their "Food" category to Plaid's FOOD_AND_DRINK taxonomy value.
        _categoryAccessor.Setup(a => a.GetByUserIdAsync(userId)).ReturnsAsync(new List<Category>
        {
            new() { Id = 8, UserId = userId, Name = "Food (Groceries + Eating Out)", PlaidCategoryPrimary = "FOOD_AND_DRINK" }
        });

        var incoming = new PlaidTransactionDto(
            TransactionId: "plaid-txn-1",
            AccountId: "plaid-acct-1",
            Amount: 12.75m,
            Date: new DateTime(2026, 4, 7),
            MerchantName: "H-E-B",
            Name: "H-E-B #123",
            Pending: false,
            PersonalFinanceCategoryPrimary: "FOOD_AND_DRINK");

        _plaidAccessor.Setup(a => a.SyncTransactionsAsync("access-token-xyz", "prev-cursor", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidSyncResult(
                Added: [incoming],
                Modified: [],
                RemovedTransactionIds: [],
                NextCursor: "next-cursor"));

        List<Transaction>? captured = null;
        _txnAccessor.Setup(a => a.UpsertImportedAsync(It.IsAny<IEnumerable<Transaction>>()))
            .Callback<IEnumerable<Transaction>>(t => captured = t.ToList())
            .ReturnsAsync((1, 0));
        _itemAccessor.Setup(a => a.UpdateSyncStateAsync(11, "next-cursor", It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var result = await BuildSut().SyncAsync(userId);

        Assert.True(result.IsSuccess);
        var mapped = Assert.Single(captured!);
        Assert.Equal(8, mapped.CategoryId);
        Assert.Equal("FOOD_AND_DRINK", mapped.PlaidCategoryPrimary);
    }

    [Fact]
    public async Task Sync_UnmappedPlaidCategory_LeavesTransactionUncategorized()
    {
        const int userId = 5;
        var plaidItem = new PlaidItem
        {
            Id = 11,
            UserId = userId,
            PlaidItemId = "plaid-item-1",
            SyncCursor = "prev-cursor",
            Accounts = [new PlaidAccount { Id = 1, PlaidAccountId = "plaid-acct-1", Name = "Plaid Checking", Mask = "0000" }]
        };

        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(userId)).ReturnsAsync([plaidItem]);
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("plaid-item-1")).ReturnsAsync(AccessTokenLookup.Found("access-token-xyz"));
        _accountAccessor.Setup(a => a.GetByUserIdAsync(userId)).ReturnsAsync(new List<Account>
        {
            new() { Id = 77, UserId = userId, Name = "Chase - Plaid Checking (••0000)", AccountType = "depository" }
        });
        // Default fixture setup: the user has mapped nothing.

        var incoming = new PlaidTransactionDto(
            TransactionId: "plaid-txn-2",
            AccountId: "plaid-acct-1",
            Amount: 12.75m,
            Date: new DateTime(2026, 4, 7),
            MerchantName: "H-E-B",
            Name: "H-E-B #123",
            Pending: false,
            PersonalFinanceCategoryPrimary: "FOOD_AND_DRINK");

        _plaidAccessor.Setup(a => a.SyncTransactionsAsync("access-token-xyz", "prev-cursor", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidSyncResult(
                Added: [incoming],
                Modified: [],
                RemovedTransactionIds: [],
                NextCursor: "next-cursor"));

        List<Transaction>? captured = null;
        _txnAccessor.Setup(a => a.UpsertImportedAsync(It.IsAny<IEnumerable<Transaction>>()))
            .Callback<IEnumerable<Transaction>>(t => captured = t.ToList())
            .ReturnsAsync((1, 0));
        _itemAccessor.Setup(a => a.UpdateSyncStateAsync(11, "next-cursor", It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var result = await BuildSut().SyncAsync(userId);

        Assert.True(result.IsSuccess);
        var mapped = Assert.Single(captured!);
        Assert.Null(mapped.CategoryId);
        // The suggestion is still persisted so it can be surfaced and accepted later.
        Assert.Equal("FOOD_AND_DRINK", mapped.PlaidCategoryPrimary);
    }

    // ── SyncAsync — multiple items + staleness ─────────────────────────────

    private static PlaidItem SyncableItem(int id, DateTime? lastSyncedAt, int userId = 5) => new()
    {
        Id = id,
        UserId = userId,
        PlaidItemId = $"item-{id}",
        SyncCursor = $"c-{id}",
        LastSyncedAt = lastSyncedAt,
        Accounts = []
    };

    private readonly Queue<(int Inserted, int Updated)> _upsertResults = new();

    /// <summary>Arranges a successful per-item sync; its upsert counts are served in call order.</summary>
    private void ArrangeItemSync(PlaidItem item, int inserted, int updated, string[] removedIds)
    {
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync(item.PlaidItemId)).ReturnsAsync(AccessTokenLookup.Found($"token-{item.Id}"));
        _accountAccessor.Setup(a => a.GetByUserIdAsync(item.UserId)).ReturnsAsync(new List<Account>());
        _plaidAccessor.Setup(a => a.SyncTransactionsAsync($"token-{item.Id}", item.SyncCursor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidSyncResult([], [], removedIds, $"next-{item.Id}"));
        _itemAccessor.Setup(a => a.UpdateSyncStateAsync(item.Id, $"next-{item.Id}", It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        if (removedIds.Length > 0)
        {
            _txnAccessor.Setup(a => a.DeleteByPlaidTransactionIdsAsync(It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(removedIds))))
                .ReturnsAsync(removedIds.Length);
        }
        _upsertResults.Enqueue((inserted, updated));
        _txnAccessor.Setup(a => a.UpsertImportedAsync(It.IsAny<IEnumerable<Transaction>>()))
            .ReturnsAsync(() => _upsertResults.Dequeue());
    }

    [Fact]
    public async Task Sync_MultipleActiveItems_SyncsEachAndAggregatesCounts()
    {
        var chase = SyncableItem(1, lastSyncedAt: null);
        var wells = SyncableItem(2, lastSyncedAt: null);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([chase, wells]);
        ArrangeItemSync(chase, inserted: 3, updated: 1, removedIds: ["r-1"]);
        ArrangeItemSync(wells, inserted: 2, updated: 4, removedIds: ["r-2", "r-3"]);

        var result = await BuildSut().SyncAsync(userId: 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.Inserted);
        Assert.Equal(5, result.Value.Updated);
        Assert.Equal(3, result.Value.Removed);
        _itemAccessor.Verify(a => a.UpdateSyncStateAsync(1, "next-1", It.IsAny<DateTime>()), Times.Once);
        _itemAccessor.Verify(a => a.UpdateSyncStateAsync(2, "next-2", It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Sync_WithStaleAfterHours_SyncsOnlyStaleItems()
    {
        var fresh = SyncableItem(1, lastSyncedAt: DateTime.UtcNow.AddHours(-1));
        var stale = SyncableItem(2, lastSyncedAt: DateTime.UtcNow.AddHours(-10));
        var neverSynced = SyncableItem(3, lastSyncedAt: null);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([fresh, stale, neverSynced]);
        ArrangeItemSync(stale, inserted: 1, updated: 0, removedIds: []);
        ArrangeItemSync(neverSynced, inserted: 2, updated: 0, removedIds: []);

        var result = await BuildSut().SyncAsync(userId: 5, staleAfterHours: 6);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Inserted);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("token-1", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("token-2", "c-2", It.IsAny<CancellationToken>()), Times.Once);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("token-3", "c-3", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sync_WithStaleAfterHours_NoneStale_ReturnsZeroSummaryWithoutCallingPlaid()
    {
        var fresh = SyncableItem(1, lastSyncedAt: DateTime.UtcNow.AddMinutes(-30));
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([fresh]);
        var before = DateTime.UtcNow;

        var result = await BuildSut().SyncAsync(userId: 5, staleAfterHours: 6);

        Assert.True(result.IsSuccess);
        Assert.Equal((0, 0, 0), (result.Value!.Inserted, result.Value.Updated, result.Value.Removed));
        Assert.InRange(result.Value.SyncedAt, before, DateTime.UtcNow);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sync_NegativeStaleAfterHours_ReturnsFailure()
    {
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([SyncableItem(1, null)]);

        var result = await BuildSut().SyncAsync(userId: 5, staleAfterHours: -1);

        Assert.False(result.IsSuccess);
        Assert.Equal("staleAfterHours cannot be negative", result.Error);
    }

    [Fact]
    public async Task Sync_OneItemThrows_OthersStillSyncAndSummaryCoversSuccesses()
    {
        // e.g. a token the current Data Protection key ring can't decrypt must not 500 the dashboard.
        var broken = SyncableItem(1, lastSyncedAt: null);
        var healthy = SyncableItem(2, lastSyncedAt: null);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([broken, healthy]);
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("item-1")).ThrowsAsync(new Exception("token unreadable"));
        ArrangeItemSync(healthy, inserted: 4, updated: 0, removedIds: []);

        var result = await BuildSut().SyncAsync(userId: 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.Inserted);
    }

    [Fact]
    public async Task Sync_OneItemUndecryptableOneOk_ReturnsSuccessWithOnlyTheHealthyItemsCounts()
    {
        var broken = SyncableItem(1, lastSyncedAt: null);
        var healthy = SyncableItem(2, lastSyncedAt: null);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([broken, healthy]);
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("item-1")).ReturnsAsync(AccessTokenLookup.Undecryptable);
        ArrangeItemSync(healthy, inserted: 4, updated: 2, removedIds: ["r-1"]);

        var result = await BuildSut().SyncAsync(userId: 5);

        Assert.True(result.IsSuccess);
        Assert.Equal((4, 2, 1), (result.Value!.Inserted, result.Value.Updated, result.Value.Removed));
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("token-1", It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _itemAccessor.Verify(a => a.UpdateSyncStateAsync(1, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        VerifyWarningLogged(Times.Once());
    }

    [Fact]
    public async Task Should_ReturnReconnectMessage_When_OnlyDueItemIsUndecryptable()
    {
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([SyncableItem(1, lastSyncedAt: null)]);
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("item-1")).ReturnsAsync(AccessTokenLookup.Undecryptable);

        var result = await BuildSut().SyncAsync(userId: 5);

        Assert.False(result.IsSuccess);
        Assert.Equal("A bank connection needs to be reconnected. Disconnect it and link it again.", result.Error);
    }

    [Fact]
    public async Task Sync_EveryDueItemFails_ReturnsFailure()
    {
        var broken = SyncableItem(1, lastSyncedAt: null);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([broken]);
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("item-1")).ReturnsAsync(AccessTokenLookup.Found("token-1"));
        _accountAccessor.Setup(a => a.GetByUserIdAsync(5)).ReturnsAsync(new List<Account>());
        _plaidAccessor.Setup(a => a.SyncTransactionsAsync("token-1", "c-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid down"));

        var result = await BuildSut().SyncAsync(userId: 5);

        Assert.False(result.IsSuccess);
        Assert.Equal("Could not refresh transactions right now. Please try again.", result.Error);
    }

    // ── GetConnectionsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetConnections_ReturnsViewWithInstitutionAndAccounts()
    {
        var plaidItem = new PlaidItem
        {
            Id = 11,
            UserId = 5,
            InstitutionName = "Chase",
            LastSyncedAt = new DateTime(2026, 1, 1),
            Accounts = [new PlaidAccount { PlaidAccountId = "plaid-acct-checking", Name = "Checking", Mask = "0000", AccountType = "depository" }]
        };
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([plaidItem]);

        var result = await BuildSut().GetConnectionsAsync(userId: 5);

        var view = Assert.Single(result);
        Assert.Equal(11, view.PlaidItemId);
        Assert.Equal("Chase", view.InstitutionName);
        Assert.Equal(new DateTime(2026, 1, 1), view.LastSyncedAt);
        Assert.Single(view.Accounts);
        Assert.Equal("plaid-acct-checking", view.Accounts[0].PlaidAccountId);
        Assert.Equal("0000", view.Accounts[0].Mask);
    }

    [Fact]
    public async Task GetConnections_MultipleAccounts_EachRetainsOwnPlaidAccountIdAndMask()
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
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([plaidItem]);

        var result = await BuildSut().GetConnectionsAsync(userId: 5);

        var view = Assert.Single(result);
        Assert.Equal(3, view.Accounts.Count);

        var byId = view.Accounts.ToDictionary(a => a.PlaidAccountId, a => a);
        Assert.Equal("0000", byId["plaid-acct-checking"].Mask);
        Assert.Equal("1111", byId["plaid-acct-savings"].Mask);
        Assert.Equal("2222", byId["plaid-acct-credit"].Mask);
        Assert.Equal("Sapphire", byId["plaid-acct-credit"].Name);
        Assert.Equal("credit", byId["plaid-acct-credit"].AccountType);
    }

    [Fact]
    public async Task GetConnections_NullMask_StillSurfacesPlaidAccountId()
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
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([plaidItem]);

        var result = await BuildSut().GetConnectionsAsync(userId: 5);

        var view = Assert.Single(result);
        Assert.Single(view.Accounts);
        Assert.Equal("plaid-acct-no-mask", view.Accounts[0].PlaidAccountId);
        Assert.Null(view.Accounts[0].Mask);
    }

    [Fact]
    public async Task GetConnections_NoAccounts_ReturnsEmptyAccountsList()
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
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([plaidItem]);

        var result = await BuildSut().GetConnectionsAsync(userId: 5);

        var view = Assert.Single(result);
        Assert.NotNull(view.Accounts);
        Assert.Empty(view.Accounts);
    }

    [Fact]
    public async Task GetConnections_NoActive_ReturnsEmptyList()
    {
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([]);

        var result = await BuildSut().GetConnectionsAsync(userId: 5);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetConnections_MultipleActiveItems_ReturnsOneViewPerItem()
    {
        var chase = new PlaidItem
        {
            Id = 11,
            UserId = 5,
            InstitutionName = "Chase",
            Accounts = [new PlaidAccount { PlaidAccountId = "chase-chk", Name = "Checking", Mask = "0000", AccountType = "depository" }]
        };
        var wells = new PlaidItem
        {
            Id = 12,
            UserId = 5,
            InstitutionName = "Wells Fargo",
            LastSyncedAt = new DateTime(2026, 9, 1),
            Accounts = [new PlaidAccount { PlaidAccountId = "wf-sav", Name = "Savings", Mask = "9999", AccountType = "depository" }]
        };
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(5)).ReturnsAsync([chase, wells]);

        var result = await BuildSut().GetConnectionsAsync(userId: 5);

        Assert.Equal(2, result.Count);
        Assert.Equal([11, 12], result.Select(v => v.PlaidItemId));
        Assert.Equal("Wells Fargo", result[1].InstitutionName);
        Assert.Equal("wf-sav", Assert.Single(result[1].Accounts).PlaidAccountId);
    }

    // ── DisconnectAsync (by id) ─────────────────────────────────────────────

    [Fact]
    public async Task Disconnect_RevokesPlaidItemAndDeactivatesOnlyThatItem()
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(5, 11)).ReturnsAsync(AccessTokenLookup.Found("access-token-xyz"));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("access-token-xyz", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _itemAccessor.Setup(a => a.DeactivateAsync(5, 11)).ReturnsAsync(true);

        var result = await BuildSut().DisconnectAsync(userId: 5, plaidItemId: 11);

        Assert.True(result.IsSuccess);
        // /item/remove is what stops Plaid's per-Item billing.
        _plaidAccessor.Verify(a => a.RemoveItemAsync("access-token-xyz", It.IsAny<CancellationToken>()), Times.Once);
        _itemAccessor.Verify(a => a.DeactivateAsync(5, 11), Times.Once);
        _itemAccessor.Verify(a => a.DeactivateAsync(It.IsAny<int>(), It.Is<int>(id => id != 11)), Times.Never);
    }

    [Fact]
    public async Task Disconnect_ItemUnknownInactiveOrOwnedByAnotherUser_ReturnsFailureWithoutSideEffects()
    {
        // The accessor scopes the lookup by (userId, plaidItemId), so another user's item, an unknown id,
        // and an already-inactive item all come back as null.
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(5, 99)).ReturnsAsync(AccessTokenLookup.NotFound);

        var result = await BuildSut().DisconnectAsync(userId: 5, plaidItemId: 99);

        Assert.False(result.IsSuccess);
        Assert.Equal("Bank connection not found", result.Error);
        _plaidAccessor.Verify(a => a.RemoveItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _itemAccessor.Verify(a => a.DeactivateAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Disconnect_PlaidRevokeFails_StillDeactivatesLocally()
    {
        // Plaid may be down or the item already revoked. We still soft-delete locally to free the user.
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(5, 11)).ReturnsAsync(AccessTokenLookup.Found("access-token-xyz"));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("access-token-xyz", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid down"));
        _itemAccessor.Setup(a => a.DeactivateAsync(5, 11)).ReturnsAsync(true);

        var result = await BuildSut().DisconnectAsync(userId: 5, plaidItemId: 11);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.DeactivateAsync(5, 11), Times.Once);
    }

    [Fact]
    public async Task Should_SkipPlaidRemoveDeactivateAndSucceed_When_TokenIsUndecryptable()
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(5, 11)).ReturnsAsync(AccessTokenLookup.Undecryptable);
        _itemAccessor.Setup(a => a.DeactivateAsync(5, 11)).ReturnsAsync(true);

        var result = await BuildSut().DisconnectAsync(userId: 5, plaidItemId: 11);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.DeactivateAsync(5, 11), Times.Once);
        _plaidAccessor.Verify(a => a.RemoveItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Should_LogWarning_When_DisconnectingAnItemWithAnUndecryptableToken()
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(5, 11)).ReturnsAsync(AccessTokenLookup.Undecryptable);
        _itemAccessor.Setup(a => a.DeactivateAsync(5, 11)).ReturnsAsync(true);

        await BuildSut().DisconnectAsync(userId: 5, plaidItemId: 11);

        VerifyWarningLogged(Times.Once());
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_TokenIsUndecryptableAndItemWasDeactivatedConcurrently()
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(5, 11)).ReturnsAsync(AccessTokenLookup.Undecryptable);
        _itemAccessor.Setup(a => a.DeactivateAsync(5, 11)).ReturnsAsync(false);

        var result = await BuildSut().DisconnectAsync(userId: 5, plaidItemId: 11);

        Assert.False(result.IsSuccess);
        Assert.Equal("Bank connection not found", result.Error);
    }

    [Fact]
    public async Task Disconnect_ItemDeactivatedConcurrently_ReturnsFailure()
    {
        // Token read succeeded but the row was deactivated between the read and the write.
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(5, 11)).ReturnsAsync(AccessTokenLookup.Found("access-token-xyz"));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("access-token-xyz", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _itemAccessor.Setup(a => a.DeactivateAsync(5, 11)).ReturnsAsync(false);

        var result = await BuildSut().DisconnectAsync(userId: 5, plaidItemId: 11);

        Assert.False(result.IsSuccess);
        Assert.Equal("Bank connection not found", result.Error);
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
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync(plaidItemId)).ReturnsAsync(AccessTokenLookup.Found("access-token-xyz"));
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
    public async Task Webhook_ItemTokenUndecryptable_ReturnsSilentSuccessAndLogsWithoutSync()
    {
        // Plaid retries non-2xx webhooks, and a lost key ring will not heal on retry: acknowledge and wait for re-link.
        var body = """{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE","item_id":"plaid-item-1"}""";
        ArrangeVerifiedWebhook(body);
        _itemAccessor.Setup(a => a.GetByPlaidItemIdAsync("plaid-item-1")).ReturnsAsync(ItemFor("plaid-item-1"));
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("plaid-item-1")).ReturnsAsync(AccessTokenLookup.Undecryptable);

        var result = await BuildSut().HandleTransactionsWebhookAsync(ValidJwt, body);

        Assert.True(result.IsSuccess);
        VerifyWarningLogged(Times.Once());
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _itemAccessor.Verify(a => a.UpdateSyncStateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
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
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync(item.PlaidItemId)).ReturnsAsync(AccessTokenLookup.Found($"token-{item.Id}"));
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
    public async Task Should_SkipUndecryptableItemWithWarningAndSyncOthers_When_Sweeping()
    {
        _options.WebhookUrl = "https://tunnel.example/api/plaid/webhook";
        var broken = new PlaidItem { Id = 1, UserId = 10, PlaidItemId = "item-broken", SyncCursor = "c-broken", Accounts = [] };
        var good = new PlaidItem { Id = 2, UserId = 20, PlaidItemId = "item-good", SyncCursor = "c-good", Accounts = [] };
        _itemAccessor.Setup(a => a.GetAllActiveAsync()).ReturnsAsync([broken, good]);
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("item-broken")).ReturnsAsync(AccessTokenLookup.Undecryptable);
        ArrangeSweepSyncForItem(good);
        _plaidAccessor.Setup(a => a.UpdateWebhookAsync("token-2", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().SweepAllAsync();

        Assert.True(result.IsSuccess);
        _plaidAccessor.Verify(a => a.SyncTransactionsAsync("token-2", "c-good", It.IsAny<CancellationToken>()), Times.Once);
        _itemAccessor.Verify(a => a.UpdateSyncStateAsync(1, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        VerifyWarningLogged(Times.Once());
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
        _itemAccessor.Setup(a => a.GetAccessTokenByPlaidItemIdAsync("plaid-item-1")).ReturnsAsync(AccessTokenLookup.Found("access-token-xyz"));
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
        _categoryAccessor.Object,
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
