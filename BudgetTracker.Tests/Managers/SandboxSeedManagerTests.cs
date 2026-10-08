using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;
using BudgetTracker.Server.Managers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BudgetTracker.Tests.Managers;

/// <summary>
/// With multiple Plaid items per user, seeding must not pile up duplicate sandbox items: it replaces
/// only the user's most recent active item (the previous seed / stock "Tartan Bank" link). The previous
/// item is taken out of the active set before the exchange (every seed shares an institution and accounts,
/// so it would otherwise be rejected as already linked) and restored if the new item never gets persisted.
/// </summary>
public class SandboxSeedManagerTests
{
    private const int UserId = 5;

    private readonly Mock<IPlaidAccessor> _plaidAccessor = new(MockBehavior.Strict);
    private readonly Mock<IPlaidManager> _plaidManager = new(MockBehavior.Strict);
    private readonly Mock<IPlaidItemAccessor> _itemAccessor = new(MockBehavior.Strict);
    private readonly Mock<IBudgetPlanAccessor> _budgetPlanAccessor = new(MockBehavior.Strict);
    private readonly Mock<ICategoryAccessor> _categoryAccessor = new(MockBehavior.Strict);
    private readonly Mock<ITransactionAccessor> _txnAccessor = new(MockBehavior.Strict);
    private readonly Mock<IAccountAccessor> _accountAccessor = new(MockBehavior.Strict);
    private readonly PlaidOptions _options = new() { Environment = "sandbox" };

    private static readonly PlaidSyncSummary SeedSummary = new(12, 0, 0, DateTime.UtcNow);

    public SandboxSeedManagerTests()
    {
        var now = DateTime.UtcNow;
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(
        [
            new BudgetPlan
            {
                Id = 1,
                UserId = UserId,
                IsActive = true,
                PlanMonth = new DateTime(now.Year, now.Month, 1),
                NetIncomeMonthly = 5000m,
                Entries = [new BudgetPlanEntry { LineType = "Expense", CategoryId = 1, MonthlyEquivalent = 400m }]
            }
        ]);
        _categoryAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([new Category { Id = 1, UserId = UserId, Name = "Food" }]);
        _plaidAccessor.Setup(a => a.CreateSandboxPublicTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("public-sandbox-token");
    }

    private SandboxSeedManager BuildSut(ILogger<SandboxSeedManager>? logger = null, IPlaidManager? plaidManager = null) => new(
        _plaidAccessor.Object,
        plaidManager ?? _plaidManager.Object,
        _itemAccessor.Object,
        _budgetPlanAccessor.Object,
        _categoryAccessor.Object,
        Options.Create(_options),
        logger ?? NullLogger<SandboxSeedManager>.Instance);

    private PlaidManager BuildRealPlaidManager() => new(
        _plaidAccessor.Object,
        _itemAccessor.Object,
        _txnAccessor.Object,
        _accountAccessor.Object,
        _categoryAccessor.Object,
        new PlaidEngine(),
        Mock.Of<IPlaidWebhookEngine>(),
        Options.Create(_options),
        NullLogger<PlaidManager>.Instance);

    private static PlaidItem Item(int id, DateTime createdAt) =>
        new() { Id = id, UserId = UserId, PlaidItemId = $"item-{id}", CreatedAt = createdAt };

    /// <summary>Arranges the previous item's token lookup and deactivation, which happen before the exchange.</summary>
    private void ArrangeTakeOffline(int plaidItemId, AccessTokenLookup lookup)
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(UserId, plaidItemId)).ReturnsAsync(lookup);
        _itemAccessor.Setup(a => a.DeactivateAsync(UserId, plaidItemId)).ReturnsAsync(true);
    }

    [Fact]
    public async Task Seed_WhenPreviousSeedIsActiveAtSameInstitution_SucceedsAndRetiresPrevious()
    {
        // Real PlaidManager + PlaidEngine: the new seed has the same institution and the same mask + name as the
        // previous one, which is exactly what IsAlreadyLinked rejects while the previous seed is still active.
        const string institutionId = "ins_109508";
        const string institutionName = "First Platypus Bank";
        var previous = new PlaidItem
        {
            Id = 12,
            UserId = UserId,
            PlaidItemId = "item-12",
            InstitutionId = institutionId,
            InstitutionName = institutionName,
            IsActive = true,
            CreatedAt = new DateTime(2026, 9, 20),
            Accounts = [new PlaidAccount { PlaidAccountId = "acct-old", Name = "Plaid Checking", Mask = "0000", AccountType = "depository" }]
        };
        var items = new List<PlaidItem> { previous };

        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId))
            .ReturnsAsync(() => items.Where(i => i.IsActive).ToList());
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(UserId, 12))
            .ReturnsAsync(() => previous.IsActive ? AccessTokenLookup.Found("token-12") : AccessTokenLookup.NotFound);
        _itemAccessor.Setup(a => a.DeactivateAsync(UserId, 12))
            .Callback(() => previous.IsActive = false)
            .ReturnsAsync(true);
        _itemAccessor.Setup(a => a.AddAsync(UserId, "access-new", "item-new", institutionId, institutionName, null, It.IsAny<IReadOnlyList<PlaidAccount>>()))
            .Callback(() => items.Add(new PlaidItem { Id = 13, UserId = UserId, PlaidItemId = "item-new", InstitutionId = institutionId, IsActive = true }))
            .ReturnsAsync(13);
        _itemAccessor.Setup(a => a.UpdateSyncStateAsync(13, "cursor-new", It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        _plaidAccessor.Setup(a => a.ExchangePublicTokenAsync("public-sandbox-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidExchangeResult("access-new", "item-new"));
        _plaidAccessor.Setup(a => a.GetItemMetadataAsync("access-new", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidItemMetadata("item-new", institutionId, institutionName, null));
        _plaidAccessor.Setup(a => a.GetAccountsAsync("access-new", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlaidAccountDto> { new("acct-new", "Plaid Checking", "0000", "depository", "checking") });
        _plaidAccessor.Setup(a => a.SyncTransactionsAsync("access-new", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidSyncResult([], [], [], "cursor-new"));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _accountAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(new List<Account>
        {
            new() { Id = 77, UserId = UserId, Name = "First Platypus Bank - Plaid Checking (••0000)", AccountType = "depository" }
        });
        _txnAccessor.Setup(a => a.UpsertImportedAsync(It.IsAny<IEnumerable<Transaction>>())).ReturnsAsync((0, 0));

        var result = await BuildSut(plaidManager: BuildRealPlaidManager()).SeedAsync(UserId, months: 2);

        Assert.True(result.IsSuccess, result.Error);
        Assert.False(previous.IsActive);
        Assert.Equal([13], items.Where(i => i.IsActive).Select(i => i.Id));
        _plaidAccessor.Verify(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>()), Times.Once);
        _plaidAccessor.Verify(a => a.RemoveItemAsync("access-new", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Seed_ExistingActiveItems_ReplacesOnlyTheMostRecentOne()
    {
        var older = Item(11, new DateTime(2026, 9, 1));
        var newest = Item(12, new DateTime(2026, 9, 20));
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([older, newest]);
        _plaidManager.Setup(m => m.ExchangePublicTokenAsync(UserId, "public-sandbox-token"))
            .ReturnsAsync(Result<PlaidSyncSummary>.Success(SeedSummary));
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().SeedAsync(UserId, months: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(SeedSummary, result.Value);
        _plaidAccessor.Verify(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>()), Times.Once);
        _itemAccessor.Verify(a => a.DeactivateAsync(UserId, 12), Times.Once);
        _itemAccessor.Verify(a => a.DeactivateAsync(UserId, 11), Times.Never);
    }

    [Fact]
    public async Task Seed_PreviousItemTieOnCreatedAt_RetiresHighestId()
    {
        var createdAt = new DateTime(2026, 9, 20, 8, 0, 0);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([Item(14, createdAt), Item(12, createdAt)]);
        _plaidManager.Setup(m => m.ExchangePublicTokenAsync(UserId, "public-sandbox-token"))
            .ReturnsAsync(Result<PlaidSyncSummary>.Success(SeedSummary));
        ArrangeTakeOffline(14, AccessTokenLookup.Found("token-14"));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("token-14", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().SeedAsync(UserId, months: 2);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.DeactivateAsync(UserId, 14), Times.Once);
        _itemAccessor.Verify(a => a.DeactivateAsync(UserId, 12), Times.Never);
        _plaidAccessor.Verify(a => a.RemoveItemAsync("token-14", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Seed_NoExistingItems_OnlyAddsTheSeededItem()
    {
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([]);
        _plaidManager.Setup(m => m.ExchangePublicTokenAsync(UserId, "public-sandbox-token"))
            .ReturnsAsync(Result<PlaidSyncSummary>.Success(SeedSummary));

        var result = await BuildSut().SeedAsync(UserId, months: 2);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.DeactivateAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Seed_ExchangeFails_ReactivatesPreviousItemWithoutRevokingIt()
    {
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([Item(12, new DateTime(2026, 9, 20))]);
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        _itemAccessor.Setup(a => a.ReactivateAsync(UserId, 12)).ReturnsAsync(true);
        _plaidManager.Setup(m => m.ExchangePublicTokenAsync(UserId, "public-sandbox-token"))
            .ReturnsAsync(Result<PlaidSyncSummary>.Failure("Could not link your bank. Please try again."));

        var result = await BuildSut().SeedAsync(UserId, months: 2);

        Assert.False(result.IsSuccess);
        Assert.Equal("Could not link your bank. Please try again.", result.Error);
        _itemAccessor.Verify(a => a.ReactivateAsync(UserId, 12), Times.Once);
        _plaidAccessor.Verify(a => a.RemoveItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Seed_ExchangeFailsAfterNewItemWasPersisted_RetiresPreviousInsteadOfReactivating()
    {
        // The initial sync can fail after AddAsync committed: the new seed exists, so restoring the previous
        // one would leave two active copies of the same accounts.
        _itemAccessor.SetupSequence(a => a.GetAllActiveByUserIdAsync(UserId))
            .ReturnsAsync([Item(12, new DateTime(2026, 9, 20))])
            .ReturnsAsync([Item(13, new DateTime(2026, 9, 27))]);
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        _plaidManager.Setup(m => m.ExchangePublicTokenAsync(UserId, "public-sandbox-token"))
            .ReturnsAsync(Result<PlaidSyncSummary>.Failure("Could not refresh transactions right now. Please try again."));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().SeedAsync(UserId, months: 2);

        Assert.False(result.IsSuccess);
        _itemAccessor.Verify(a => a.ReactivateAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _plaidAccessor.Verify(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Seed_ExchangeFailsAndReactivationThrows_ReturnsExchangeFailureAndLogsError()
    {
        var logger = new Mock<ILogger<SandboxSeedManager>>();
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([Item(12, new DateTime(2026, 9, 20))]);
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        _itemAccessor.Setup(a => a.ReactivateAsync(UserId, 12)).ThrowsAsync(new InvalidOperationException("db down"));
        _plaidManager.Setup(m => m.ExchangePublicTokenAsync(UserId, "public-sandbox-token"))
            .ReturnsAsync(Result<PlaidSyncSummary>.Failure("Could not link your bank. Please try again."));

        var result = await BuildSut(logger.Object).SeedAsync(UserId, months: 2);

        Assert.False(result.IsSuccess);
        Assert.Equal("Could not link your bank. Please try again.", result.Error);
        _plaidAccessor.Verify(a => a.RemoveItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<InvalidOperationException>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Seed_RevokingPreviousItemFails_StillDeactivatesItAndSucceeds()
    {
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([Item(12, new DateTime(2026, 9, 20))]);
        _plaidManager.Setup(m => m.ExchangePublicTokenAsync(UserId, "public-sandbox-token"))
            .ReturnsAsync(Result<PlaidSyncSummary>.Success(SeedSummary));
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid down"));

        var result = await BuildSut().SeedAsync(UserId, months: 2);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.DeactivateAsync(UserId, 12), Times.Once);
    }

    [Fact]
    public async Task Seed_PreviousItemTokenUndecryptable_SkipsRemoveButStillDeactivates()
    {
        var logger = new Mock<ILogger<SandboxSeedManager>>();
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([Item(12, new DateTime(2026, 9, 20))]);
        _plaidManager.Setup(m => m.ExchangePublicTokenAsync(UserId, "public-sandbox-token"))
            .ReturnsAsync(Result<PlaidSyncSummary>.Success(SeedSummary));
        ArrangeTakeOffline(12, AccessTokenLookup.Undecryptable);

        var result = await BuildSut(logger.Object).SeedAsync(UserId, months: 2);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.DeactivateAsync(UserId, 12), Times.Once);
        _itemAccessor.Verify(a => a.ReactivateAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _plaidAccessor.Verify(a => a.RemoveItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
