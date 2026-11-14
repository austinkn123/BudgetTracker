using BudgetTracker.Domain.Common;
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

/// <summary>
/// Sandbox seeding and login reset on <see cref="PlaidManager"/>. With multiple Plaid items per user, seeding must not
/// pile up duplicate sandbox items: it replaces only the item the engine selects (the previous seed / stock "Tartan
/// Bank" link). The previous item is taken out of the active set before the exchange (every seed shares an institution
/// and accounts, so it would otherwise be rejected as already linked) and restored if the new item never gets persisted.
/// The seed always runs the real link path, so its Plaid and item accessors are arranged here.
/// </summary>
public class PlaidManagerSandboxSeedTests
{
    private const int UserId = 5;
    private const string PublicToken = "public-sandbox-token";
    private const string SeedConfig = "seed-config";
    private const string InstitutionId = "ins_109508";
    private const string InstitutionName = "First Platypus Bank";
    private const string LinkFailedMessage = "Could not link your bank. Please try again.";

    private readonly Mock<IPlaidAccessor> _plaidAccessor = new(MockBehavior.Strict);
    private readonly Mock<IPlaidItemAccessor> _itemAccessor = new(MockBehavior.Strict);
    private readonly Mock<IBudgetPlanAccessor> _budgetPlanAccessor = new(MockBehavior.Strict);
    private readonly Mock<ICategoryAccessor> _categoryAccessor = new(MockBehavior.Strict);
    private readonly Mock<ITransactionAccessor> _txnAccessor = new(MockBehavior.Strict);
    private readonly Mock<IAccountAccessor> _accountAccessor = new(MockBehavior.Strict);
    private readonly Mock<IDataVersionAccessor> _dataVersion = new(MockBehavior.Strict);
    private readonly Mock<ISandboxSeedEngine> _seedEngine = new(MockBehavior.Strict);
    private readonly List<string> _calls = [];
    private readonly PlaidOptions _options = new() { Environment = "sandbox" };

    private static readonly IReadOnlyList<SandboxSeedLine> SeedLines = [new("H-E-B", 400m, 8)];

    public PlaidManagerSandboxSeedTests()
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

        _seedEngine.Setup(e => e.ValidateMonths(It.IsAny<int>())).Returns((string?)null);
        _seedEngine.Setup(e => e.BuildLines(It.IsAny<BudgetPlan>(), It.IsAny<IReadOnlyDictionary<int, string>>())).Returns(SeedLines);
        _seedEngine.Setup(e => e.BuildConfig(SeedLines, 5000m, It.IsAny<DateOnly>(), It.IsAny<DateOnly>())).Returns(SeedConfig);
        _seedEngine.Setup(e => e.SelectItemToReplace(It.IsAny<IReadOnlyList<PlaidItem>>()))
            .Returns<IReadOnlyList<PlaidItem>>(items => items.FirstOrDefault());

        _plaidAccessor.Setup(a => a.CreateSandboxPublicTokenAsync(SeedConfig, It.IsAny<CancellationToken>())).ReturnsAsync(PublicToken);
        _txnAccessor.Setup(a => a.GetTransferCandidatesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync([]);
        _dataVersion.Setup(d => d.BumpAsync(It.IsAny<int>()))
            .Callback<int>(userId => _calls.Add($"bump:{userId}"))
            .ReturnsAsync(true);
    }

    private PlaidManager BuildSut(ILogger<PlaidManager>? logger = null) => new(
        _plaidAccessor.Object,
        _itemAccessor.Object,
        _txnAccessor.Object,
        _accountAccessor.Object,
        _categoryAccessor.Object,
        new PlaidEngine(),
        Mock.Of<IPlaidWebhookEngine>(),
        _dataVersion.Object,
        _budgetPlanAccessor.Object,
        new BudgetPlanEngine(),
        _seedEngine.Object,
        Options.Create(_options),
        logger ?? NullLogger<PlaidManager>.Instance);

    private static PlaidItem Item(int id, DateTime createdAt) =>
        new() { Id = id, UserId = UserId, PlaidItemId = $"item-{id}", CreatedAt = createdAt, IsActive = true };

    /// <summary>Arranges the previous item's token lookup and deactivation, which happen before the exchange.</summary>
    private void ArrangeTakeOffline(int plaidItemId, AccessTokenLookup lookup)
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(UserId, plaidItemId)).ReturnsAsync(lookup);
        _itemAccessor.Setup(a => a.DeactivateAsync(UserId, plaidItemId)).ReturnsAsync(true);
    }

    /// <summary>Arranges the real link path up to and including persisting the new item 13.</summary>
    private void ArrangeLinkPersists()
    {
        _plaidAccessor.Setup(a => a.ExchangePublicTokenAsync(PublicToken, It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("exchange"))
            .ReturnsAsync(new PlaidExchangeResult("access-new", "item-new"));
        _plaidAccessor.Setup(a => a.GetItemMetadataAsync("access-new", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidItemMetadata("item-new", InstitutionId, InstitutionName, null));
        _plaidAccessor.Setup(a => a.GetAccountsAsync("access-new", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlaidAccountDto> { new("acct-new", "Plaid Checking", "0000", "depository", "checking") });
        _accountAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(new List<Account>
        {
            new() { Id = 77, UserId = UserId, Name = "First Platypus Bank - Plaid Checking (••0000)", AccountType = "depository" }
        });
        _itemAccessor.Setup(a => a.AddAsync(UserId, "access-new", "item-new", InstitutionId, InstitutionName, null, It.IsAny<IReadOnlyList<PlaidAccount>>()))
            .ReturnsAsync(13);
    }

    /// <summary>Arranges a link whose exchange, persist and initial sync all succeed with 12 inserted rows.</summary>
    private void ArrangeLinkSucceeds()
    {
        ArrangeLinkPersists();
        _plaidAccessor.Setup(a => a.SyncTransactionsAsync("access-new", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidSyncResult([], [], [], "cursor-new"));
        _itemAccessor.Setup(a => a.UpdateSyncStateAsync(13, "cursor-new", It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _txnAccessor.Setup(a => a.UpsertImportedAsync(It.IsAny<IEnumerable<Transaction>>())).ReturnsAsync((12, 0));
    }

    /// <summary>Arranges a link that fails at the exchange, before anything is persisted.</summary>
    private void ArrangeExchangeFails() =>
        _plaidAccessor.Setup(a => a.ExchangePublicTokenAsync(PublicToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid down"));

    // ── SeedSandboxAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task Seed_WhenPreviousSeedIsActiveAtSameInstitution_SucceedsAndRetiresPrevious()
    {
        // Real PlaidEngine: the new seed has the same institution and the same mask + name as the previous one,
        // which is exactly what IsAlreadyLinked rejects while the previous seed is still active.
        var previous = new PlaidItem
        {
            Id = 12,
            UserId = UserId,
            PlaidItemId = "item-12",
            InstitutionId = InstitutionId,
            InstitutionName = InstitutionName,
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
        ArrangeLinkSucceeds();
        _itemAccessor.Setup(a => a.AddAsync(UserId, "access-new", "item-new", InstitutionId, InstitutionName, null, It.IsAny<IReadOnlyList<PlaidAccount>>()))
            .Callback(() => items.Add(new PlaidItem { Id = 13, UserId = UserId, PlaidItemId = "item-new", InstitutionId = InstitutionId, IsActive = true }))
            .ReturnsAsync(13);
        _plaidAccessor.Setup(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().SeedSandboxAsync(UserId, months: 2);

        Assert.True(result.IsSuccess, result.Error);
        Assert.False(previous.IsActive);
        Assert.Equal([13], items.Where(i => i.IsActive).Select(i => i.Id));
        _plaidAccessor.Verify(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>()), Times.Once);
        _plaidAccessor.Verify(a => a.RemoveItemAsync("access-new", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Seed_ExistingActiveItems_ReplacesOnlyTheSelectedOne()
    {
        var older = Item(11, new DateTime(2026, 9, 1));
        var newest = Item(12, new DateTime(2026, 9, 20));
        IReadOnlyList<PlaidItem> active = [older, newest];
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync(active);
        _seedEngine.Setup(e => e.SelectItemToReplace(active)).Returns(newest);
        ArrangeLinkSucceeds();
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().SeedSandboxAsync(UserId, months: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value!.Inserted);
        _plaidAccessor.Verify(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>()), Times.Once);
        _itemAccessor.Verify(a => a.DeactivateAsync(UserId, 12), Times.Once);
        _itemAccessor.Verify(a => a.DeactivateAsync(UserId, 11), Times.Never);
    }

    [Fact]
    public async Task Seed_PreviousItemTieOnCreatedAt_RetiresTheEngineChoice()
    {
        // The tie-break itself (highest id) is SandboxSeedEngineTests' job; this pins that the manager hands the engine
        // every active item and retires exactly the one it picks.
        var createdAt = new DateTime(2026, 9, 20, 8, 0, 0);
        IReadOnlyList<PlaidItem> active = [Item(14, createdAt), Item(12, createdAt)];
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync(active);
        _seedEngine.Setup(e => e.SelectItemToReplace(active)).Returns(active[0]);
        ArrangeLinkSucceeds();
        ArrangeTakeOffline(14, AccessTokenLookup.Found("token-14"));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("token-14", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().SeedSandboxAsync(UserId, months: 2);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.DeactivateAsync(UserId, 14), Times.Once);
        _itemAccessor.Verify(a => a.DeactivateAsync(UserId, 12), Times.Never);
        _plaidAccessor.Verify(a => a.RemoveItemAsync("token-14", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Seed_NoExistingItems_OnlyAddsTheSeededItem()
    {
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([]);
        ArrangeLinkSucceeds();

        var result = await BuildSut().SeedSandboxAsync(UserId, months: 2);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.AddAsync(UserId, "access-new", "item-new", InstitutionId, InstitutionName, null, It.IsAny<IReadOnlyList<PlaidAccount>>()), Times.Once);
        _itemAccessor.Verify(a => a.DeactivateAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Seed_ExchangeFails_ReactivatesPreviousItemWithoutRevokingIt()
    {
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([Item(12, new DateTime(2026, 9, 20))]);
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        _itemAccessor.Setup(a => a.ReactivateAsync(UserId, 12)).ReturnsAsync(true);
        ArrangeExchangeFails();

        var result = await BuildSut().SeedSandboxAsync(UserId, months: 2);

        Assert.False(result.IsSuccess);
        Assert.Equal(LinkFailedMessage, result.Error);
        _itemAccessor.Verify(a => a.ReactivateAsync(UserId, 12), Times.Once);
        _plaidAccessor.Verify(a => a.RemoveItemAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Seed_ExchangeFailsAfterNewItemWasPersisted_RetiresPreviousInsteadOfReactivating()
    {
        // The initial sync can fail after AddAsync committed: the new seed exists, so restoring the previous
        // one would leave two active copies of the same accounts.
        var previous = Item(12, new DateTime(2026, 9, 20));
        _itemAccessor.SetupSequence(a => a.GetAllActiveByUserIdAsync(UserId))
            .ReturnsAsync([previous])                          // captured before the exchange
            .ReturnsAsync([previous])                          // the link's already-linked check
            .ReturnsAsync([Item(13, new DateTime(2026, 9, 27))]);
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        ArrangeLinkPersists();
        _plaidAccessor.Setup(a => a.SyncTransactionsAsync("access-new", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid down"));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().SeedSandboxAsync(UserId, months: 2);

        Assert.False(result.IsSuccess);
        Assert.Equal("Could not refresh transactions right now. Please try again.", result.Error);
        _itemAccessor.Verify(a => a.ReactivateAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _plaidAccessor.Verify(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Seed_ExchangeFailsAndReactivationThrows_ReturnsExchangeFailureAndLogsError()
    {
        var logger = new Mock<ILogger<PlaidManager>>();
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([Item(12, new DateTime(2026, 9, 20))]);
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        _itemAccessor.Setup(a => a.ReactivateAsync(UserId, 12)).ThrowsAsync(new InvalidOperationException("db down"));
        ArrangeExchangeFails();

        var result = await BuildSut(logger.Object).SeedSandboxAsync(UserId, months: 2);

        Assert.False(result.IsSuccess);
        Assert.Equal(LinkFailedMessage, result.Error);
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
        ArrangeLinkSucceeds();
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid down"));

        var result = await BuildSut().SeedSandboxAsync(UserId, months: 2);

        Assert.True(result.IsSuccess);
        _itemAccessor.Verify(a => a.DeactivateAsync(UserId, 12), Times.Once);
    }

    [Fact]
    public async Task Seed_PreviousItemTokenUndecryptable_SkipsRemoveButStillDeactivates()
    {
        var logger = new Mock<ILogger<PlaidManager>>();
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([Item(12, new DateTime(2026, 9, 20))]);
        ArrangeLinkSucceeds();
        ArrangeTakeOffline(12, AccessTokenLookup.Undecryptable);

        var result = await BuildSut(logger.Object).SeedSandboxAsync(UserId, months: 2);

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

    [Theory]
    [InlineData("production")]
    [InlineData("development")]
    [InlineData("")]
    public async Task Seed_NotSandbox_RefusesWithoutTouchingPlaidOrThePlan(string environment)
    {
        _options.Environment = environment;

        var result = await BuildSut().SeedSandboxAsync(UserId, months: 2);

        Assert.False(result.IsSuccess);
        Assert.Equal("Sandbox seeding is only available when Plaid is configured for sandbox.", result.Error);
        _budgetPlanAccessor.Verify(a => a.GetByUserIdAsync(It.IsAny<int>()), Times.Never);
        _plaidAccessor.Verify(a => a.CreateSandboxPublicTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Seed_MonthsRejected_ReturnsEngineErrorWithoutCallingPlaid()
    {
        _seedEngine.Setup(e => e.ValidateMonths(13)).Returns("Months must be between 1 and 12");

        var result = await BuildSut().SeedSandboxAsync(UserId, months: 13);

        Assert.False(result.IsSuccess);
        Assert.Equal("Months must be between 1 and 12", result.Error);
        _budgetPlanAccessor.Verify(a => a.GetByUserIdAsync(It.IsAny<int>()), Times.Never);
        _plaidAccessor.Verify(a => a.CreateSandboxPublicTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _plaidAccessor.Verify(a => a.ExchangePublicTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Seed_NoEffectivePlan_FailsWithoutCallingPlaid()
    {
        var nextMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(
        [
            new BudgetPlan { Id = 2, UserId = UserId, IsActive = true, PlanMonth = nextMonth },
            new BudgetPlan { Id = 3, UserId = UserId, IsActive = false, PlanMonth = nextMonth.AddMonths(-3) }
        ]);

        var result = await BuildSut().SeedSandboxAsync(UserId, months: 2);

        Assert.False(result.IsSuccess);
        Assert.Equal("Create an active budget plan first — the seed is derived from it.", result.Error);
        _plaidAccessor.Verify(a => a.CreateSandboxPublicTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Seed_PlanHasNoSeedLines_FailsWithoutCallingPlaid()
    {
        _seedEngine.Setup(e => e.BuildLines(It.IsAny<BudgetPlan>(), It.IsAny<IReadOnlyDictionary<int, string>>())).Returns([]);

        var result = await BuildSut().SeedSandboxAsync(UserId, months: 2);

        Assert.False(result.IsSuccess);
        Assert.Equal("The active plan has no expense entries to derive transactions from.", result.Error);
        _plaidAccessor.Verify(a => a.CreateSandboxPublicTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Data version (live refresh) ─────────────────────────────────────────

    private void VerifyNeverBumped() => _dataVersion.Verify(d => d.BumpAsync(It.IsAny<int>()), Times.Never);

    [Fact]
    public async Task Seed_PreviousItemRetired_BumpsOnceAtTheEnd()
    {
        // The link path bumps twice for what it writes (item persisted, initial sync); the seed adds one after the revoke.
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([Item(12, new DateTime(2026, 9, 20))]);
        ArrangeLinkSucceeds();
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        _plaidAccessor.Setup(a => a.RemoveItemAsync("token-12", It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("revoke"))
            .Returns(Task.CompletedTask);

        await BuildSut().SeedSandboxAsync(UserId, months: 2);

        Assert.Equal(["exchange", $"bump:{UserId}", $"bump:{UserId}", "revoke", $"bump:{UserId}"], _calls);
    }

    [Fact]
    public async Task Seed_ExchangeFailsAndPreviousIsRestored_StillBumpsOnceAfterTheRestore()
    {
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([Item(12, new DateTime(2026, 9, 20))]);
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        _itemAccessor.Setup(a => a.ReactivateAsync(UserId, 12)).Callback(() => _calls.Add("restore")).ReturnsAsync(true);
        ArrangeExchangeFails();

        await BuildSut().SeedSandboxAsync(UserId, months: 2);

        Assert.Equal(["restore", $"bump:{UserId}"], _calls);
    }

    [Fact]
    public async Task Seed_ExchangeThrowsAfterThePreviousWasDeactivated_StillBumps()
    {
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([Item(12, new DateTime(2026, 9, 20))]);
        ArrangeTakeOffline(12, AccessTokenLookup.Found("token-12"));
        ArrangeLinkPersists();
        _accountAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildSut().SeedSandboxAsync(UserId, months: 2));

        Assert.Equal(["exchange", $"bump:{UserId}"], _calls);
    }

    [Fact]
    public async Task Seed_NoPreviousItem_LeavesTheBumpToTheExchange()
    {
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([]);
        ArrangeLinkSucceeds();

        await BuildSut().SeedSandboxAsync(UserId, months: 2);

        // Only the link path's own bumps (item persisted, initial sync); the seed adds none of its own.
        Assert.Equal(["exchange", $"bump:{UserId}", $"bump:{UserId}"], _calls);
    }

    [Fact]
    public async Task Seed_RejectedBeforeAnyWrite_DoesNotBump()
    {
        _seedEngine.Setup(e => e.ValidateMonths(0)).Returns("Months must be between 1 and 12");
        await BuildSut().SeedSandboxAsync(UserId, months: 0);
        _budgetPlanAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);
        await BuildSut().SeedSandboxAsync(UserId, months: 2);
        _options.Environment = "production";
        await BuildSut().SeedSandboxAsync(UserId, months: 2);

        VerifyNeverBumped();
    }

    [Fact]
    public async Task ResetLogin_DoesNotBump()
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(UserId, 12)).ReturnsAsync(AccessTokenLookup.Found("token-12"));
        _plaidAccessor.Setup(a => a.ResetSandboxLoginAsync("token-12", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().ResetSandboxLoginAsync(UserId, 12);

        Assert.True(result.IsSuccess);
        VerifyNeverBumped();
    }

    // ── ResetSandboxLoginAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ResetLogin_SandboxItemFound_ResetsItsLoginAtPlaid()
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(UserId, 11)).ReturnsAsync(AccessTokenLookup.Found("access-token-xyz"));
        _plaidAccessor.Setup(a => a.ResetSandboxLoginAsync("access-token-xyz", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await BuildSut().ResetSandboxLoginAsync(UserId, 11);

        Assert.True(result.IsSuccess);
        _plaidAccessor.Verify(a => a.ResetSandboxLoginAsync("access-token-xyz", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("production")]
    [InlineData("development")]
    [InlineData("")]
    public async Task ResetLogin_NotSandbox_RefusesWithoutTouchingTheItem(string environment)
    {
        _options.Environment = environment;

        var result = await BuildSut().ResetSandboxLoginAsync(UserId, 11);

        Assert.False(result.IsSuccess);
        Assert.Equal("Sandbox login reset is only available when Plaid is configured for sandbox.", result.Error);
        _itemAccessor.Verify(a => a.GetActiveAccessTokenAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ResetLogin_ItemUnknownInactiveOrOwnedByAnotherUser_ReturnsNotFound()
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(UserId, 99)).ReturnsAsync(AccessTokenLookup.NotFound);

        var result = await BuildSut().ResetSandboxLoginAsync(UserId, 99);

        Assert.False(result.IsSuccess);
        Assert.Equal("Bank connection not found", result.Error);
        _plaidAccessor.Verify(a => a.ResetSandboxLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetLogin_TokenUndecryptable_ReturnsFailureWithoutCallingPlaid()
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(UserId, 11)).ReturnsAsync(AccessTokenLookup.Undecryptable);

        var result = await BuildSut().ResetSandboxLoginAsync(UserId, 11);

        Assert.False(result.IsSuccess);
        _plaidAccessor.Verify(a => a.ResetSandboxLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetLogin_PlaidThrows_ReturnsFailureWithoutLeakingDetails()
    {
        _itemAccessor.Setup(a => a.GetActiveAccessTokenAsync(UserId, 11)).ReturnsAsync(AccessTokenLookup.Found("access-token-xyz"));
        _plaidAccessor.Setup(a => a.ResetSandboxLoginAsync("access-token-xyz", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Plaid API error (status 400): INVALID_PRODUCT"));

        var result = await BuildSut().ResetSandboxLoginAsync(UserId, 11);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("INVALID_PRODUCT", result.Error!);
    }
}
