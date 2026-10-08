using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Tests.Engines;

public class PlaidEngineTests
{
    private readonly PlaidEngine _sut = new();

    private static PlaidTransactionDto BuildPlaidDebit(decimal amount = 4.50m, bool pending = false) => new(
        TransactionId: "plaid-txn-1",
        AccountId: "plaid-acct-1",
        Amount: amount,
        Date: DateTime.UtcNow.Date.AddDays(-1),
        MerchantName: "Starbucks",
        Name: "STARBUCKS #1234",
        Pending: pending,
        PersonalFinanceCategoryPrimary: "FOOD_AND_DRINK");

    private static PlaidTransactionDto BuildPlaidCredit(decimal amount = -2000m) => new(
        TransactionId: "plaid-txn-2",
        AccountId: "plaid-acct-1",
        Amount: amount,
        Date: DateTime.UtcNow.Date.AddDays(-2),
        MerchantName: "ACME PAYROLL",
        Name: "ACME PAYROLL DIRECT DEP",
        Pending: false,
        PersonalFinanceCategoryPrimary: "INCOME");

    // ── Sign inversion: Plaid positive (debit/outflow) → BudgetTracker negative Expense ─

    [Fact]
    public void Map_PlaidPositiveAmount_InvertsToNegativeExpense()
    {
        var dto = BuildPlaidDebit(amount: 12.34m);

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7);

        Assert.Equal(-12.34m, result.Amount);
        Assert.Equal("Expense", result.TransactionType);
    }

    [Fact]
    public void Map_PlaidNegativeAmount_InvertsToPositiveIncome()
    {
        var dto = BuildPlaidCredit(amount: -2500m);

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7);

        Assert.Equal(2500m, result.Amount);
        Assert.Equal("Income", result.TransactionType);
    }

    // ── Field mapping ───────────────────────────────────────────────────────

    [Fact]
    public void Map_PreservesMerchantNameAsPayee()
    {
        var dto = BuildPlaidDebit() with { MerchantName = "Whole Foods" };

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7);

        Assert.Equal("Whole Foods", result.Payee);
    }

    [Fact]
    public void Map_FallsBackToTransactionNameWhenMerchantIsNull()
    {
        var dto = BuildPlaidDebit() with { MerchantName = null, Name = "POS PURCHASE #5678" };

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7);

        Assert.Equal("POS PURCHASE #5678", result.Payee);
    }

    [Fact]
    public void Map_CopiesDateAndAccountId()
    {
        var date = new DateTime(2025, 10, 15);
        var dto = BuildPlaidDebit() with { Date = date };

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 42);

        Assert.Equal(date, result.OccurredAt);
        Assert.Equal(42, result.AccountId);
    }

    // ── Plaid category suggestion (BUD-9) ───────────────────────────────────

    [Fact]
    public void Map_PersistsPlaidCategorySuggestion()
    {
        var dto = BuildPlaidDebit();

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7);

        Assert.Equal("FOOD_AND_DRINK", result.PlaidCategoryPrimary);
    }

    [Fact]
    public void Map_AppliesResolvedCategoryId()
    {
        var dto = BuildPlaidDebit();

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7, categoryId: 99);

        Assert.Equal(99, result.CategoryId);
        // The raw suggestion survives alongside the resolved category so an override stays traceable.
        Assert.Equal("FOOD_AND_DRINK", result.PlaidCategoryPrimary);
    }

    [Fact]
    public void Map_WithoutResolvedCategory_LeavesCategoryNull()
    {
        var dto = BuildPlaidDebit();

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7);

        Assert.Null(result.CategoryId);
    }

    [Fact]
    public void ResolveCategoryId_MatchesMappedCategory()
    {
        var dto = BuildPlaidDebit();
        var categories = new[]
        {
            new Category { Id = 3, Name = "Rent", PlaidCategoryPrimary = "RENT_AND_UTILITIES" },
            new Category { Id = 8, Name = "Food (Groceries + Eating Out)", PlaidCategoryPrimary = "FOOD_AND_DRINK" }
        };

        Assert.Equal(8, _sut.ResolveCategoryId(dto, categories));
    }

    [Fact]
    public void ResolveCategoryId_IsCaseInsensitive()
    {
        var dto = BuildPlaidDebit() with { PersonalFinanceCategoryPrimary = "food_and_drink" };
        var categories = new[] { new Category { Id = 8, PlaidCategoryPrimary = "FOOD_AND_DRINK" } };

        Assert.Equal(8, _sut.ResolveCategoryId(dto, categories));
    }

    [Fact]
    public void ResolveCategoryId_NoMappingConfigured_ReturnsNull()
    {
        var dto = BuildPlaidDebit();
        var categories = new[] { new Category { Id = 3, Name = "Rent", PlaidCategoryPrimary = null } };

        Assert.Null(_sut.ResolveCategoryId(dto, categories));
    }

    [Fact]
    public void ResolveCategoryId_PlaidSentNoSuggestion_ReturnsNull()
    {
        var dto = BuildPlaidDebit() with { PersonalFinanceCategoryPrimary = null };
        var categories = new[] { new Category { Id = 8, PlaidCategoryPrimary = "FOOD_AND_DRINK" } };

        Assert.Null(_sut.ResolveCategoryId(dto, categories));
    }

    [Fact]
    public void ResolveCategoryId_EmptyCategories_ReturnsNull()
    {
        Assert.Null(_sut.ResolveCategoryId(BuildPlaidDebit(), Array.Empty<Category>()));
    }

    [Fact]
    public void Map_StampsPlaidIdentifiersAndImportedFlag()
    {
        var dto = BuildPlaidDebit();

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7);

        Assert.Equal("plaid-txn-1", result.PlaidTransactionId);
        Assert.Equal("plaid-acct-1", result.PlaidAccountId);
        Assert.True(result.IsImported);
    }

    [Fact]
    public void Map_PropagatesPendingFlag()
    {
        var dto = BuildPlaidDebit(pending: true);

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7);

        Assert.True(result.IsPending);
    }

    [Fact]
    public void Map_PostedTransaction_HasPendingFalse()
    {
        var dto = BuildPlaidDebit(pending: false);

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7);

        Assert.False(result.IsPending);
    }

    // ── Boundary: zero amount cannot occur in Plaid practice, but guard anyway ─

    [Fact]
    public void Map_ZeroAmount_MapsToExpenseAndPreservesZero()
    {
        // Defensive: Plaid will never emit zero, but if it does we don't want to crash —
        // the downstream CK_Transactions_NonZeroAmount constraint will reject it loudly.
        var dto = BuildPlaidDebit(amount: 0m);

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7);

        Assert.Equal(0m, result.Amount);
    }

    // ── BuildBudgetTrackerAccount ───────────────────────────────────────────

    [Fact]
    public void BuildBudgetTrackerAccount_ProducesDisplayNameAndAccountType()
    {
        var plaidAccount = new PlaidAccountDto(
            AccountId: "plaid-acct-1",
            Name: "Plaid Checking",
            Mask: "0000",
            Type: "depository",
            Subtype: "checking");

        var account = _sut.BuildBudgetTrackerAccount(plaidAccount, "Chase", userId: 5);

        Assert.Equal(5, account.UserId);
        Assert.Equal("Chase - Plaid Checking (••0000)", account.Name);
        Assert.Equal("depository", account.AccountType);
    }

    [Fact]
    public void BuildBudgetTrackerAccount_OmitsMaskParensWhenMaskMissing()
    {
        var plaidAccount = new PlaidAccountDto(
            AccountId: "plaid-acct-2",
            Name: "Savings",
            Mask: null,
            Type: "depository",
            Subtype: "savings");

        var account = _sut.BuildBudgetTrackerAccount(plaidAccount, "Chase", userId: 5);

        Assert.Equal("Chase - Savings", account.Name);
    }

    // ── ResolveBudgetTrackerAccountId ───────────────────────────────────────

    [Fact]
    public void Resolve_ReturnsExistingAccountIdWhenNameMatches()
    {
        var plaidAccount = new PlaidAccountDto("plaid-acct-1", "Plaid Checking", "0000", "depository", "checking");
        var existing = new Account { Id = 99, UserId = 5, Name = "Chase - Plaid Checking (••0000)", AccountType = "depository" };

        var result = _sut.ResolveBudgetTrackerAccountId(plaidAccount, [existing]);

        Assert.Equal(99, result);
    }

    [Fact]
    public void Resolve_ReturnsNullWhenNoMatch()
    {
        var plaidAccount = new PlaidAccountDto("plaid-acct-1", "Plaid Checking", "0000", "depository", "checking");
        var unrelated = new Account { Id = 99, UserId = 5, Name = "Cash", AccountType = "depository" };

        var result = _sut.ResolveBudgetTrackerAccountId(plaidAccount, [unrelated]);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_EmptyUserAccounts_ReturnsNull()
    {
        var plaidAccount = new PlaidAccountDto("plaid-acct-1", "Plaid Checking", "0000", "depository", "checking");

        var result = _sut.ResolveBudgetTrackerAccountId(plaidAccount, []);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("0000")]
    [InlineData(null)]
    public void Resolve_ExistingAccountNamedLikeTheNewOneIgnoringCase_ReturnsItsId(string? mask)
    {
        // UQ_Accounts_User_Name is on lower("Name"): an account whose built name collides ignoring case must be reused,
        // never created, or the link fails with a unique-index violation.
        var plaidAccount = new PlaidAccountDto("plaid-acct-1", "Plaid Checking", mask, "depository", "checking");
        var builtName = _sut.BuildBudgetTrackerAccount(plaidAccount, "Chase", 5).Name;
        var existing = new Account { Id = 99, UserId = 5, Name = builtName.ToLowerInvariant(), AccountType = "depository" };

        var result = _sut.ResolveBudgetTrackerAccountId(plaidAccount, [existing]);

        Assert.Equal(99, result);
    }

    // ── SelectItemsDueForSync: stale-filtered sync on dashboard open ────────

    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    private static PlaidItem ItemSyncedAt(int id, DateTime? lastSyncedAt) =>
        new() { Id = id, PlaidItemId = $"item-{id}", LastSyncedAt = lastSyncedAt };

    [Fact]
    public void SelectDue_NoThreshold_ReturnsEveryItem()
    {
        var items = new[] { ItemSyncedAt(1, Now.AddMinutes(-1)), ItemSyncedAt(2, null) };

        var result = _sut.SelectItemsDueForSync(items, Now, staleAfterHours: null);

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2], result.Value!.Select(i => i.Id));
    }

    [Fact]
    public void SelectDue_NeverSyncedItem_IsDue()
    {
        var result = _sut.SelectItemsDueForSync([ItemSyncedAt(1, null)], Now, staleAfterHours: 6);

        Assert.Equal(1, Assert.Single(result.Value!).Id);
    }

    [Fact]
    public void SelectDue_ItemOlderThanThreshold_IsDue()
    {
        var result = _sut.SelectItemsDueForSync([ItemSyncedAt(1, Now.AddHours(-7))], Now, staleAfterHours: 6);

        Assert.Single(result.Value!);
    }

    [Fact]
    public void SelectDue_ItemFresherThanThreshold_IsSkipped()
    {
        var result = _sut.SelectItemsDueForSync([ItemSyncedAt(1, Now.AddHours(-5))], Now, staleAfterHours: 6);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void SelectDue_ItemExactlyAtThreshold_IsSkipped()
    {
        // "Older than N hours" is strict: exactly N hours old is still fresh.
        var result = _sut.SelectItemsDueForSync([ItemSyncedAt(1, Now.AddHours(-6))], Now, staleAfterHours: 6);

        Assert.Empty(result.Value!);
    }

    [Fact]
    public void SelectDue_ItemOneTickPastThreshold_IsDue()
    {
        var result = _sut.SelectItemsDueForSync([ItemSyncedAt(1, Now.AddHours(-6).AddTicks(-1))], Now, staleAfterHours: 6);

        Assert.Single(result.Value!);
    }

    [Fact]
    public void SelectDue_MixedItems_ReturnsOnlyStaleOnes()
    {
        var items = new[]
        {
            ItemSyncedAt(1, Now.AddHours(-1)),
            ItemSyncedAt(2, Now.AddHours(-12)),
            ItemSyncedAt(3, null)
        };

        var result = _sut.SelectItemsDueForSync(items, Now, staleAfterHours: 6);

        Assert.Equal([2, 3], result.Value!.Select(i => i.Id));
    }

    [Fact]
    public void SelectDue_ZeroThreshold_ReturnsAnyPreviouslySyncedItem()
    {
        var result = _sut.SelectItemsDueForSync([ItemSyncedAt(1, Now.AddSeconds(-1))], Now, staleAfterHours: 0);

        Assert.Single(result.Value!);
    }

    [Fact]
    public void SelectDue_NegativeThreshold_ReturnsFailure()
    {
        var result = _sut.SelectItemsDueForSync([ItemSyncedAt(1, null)], Now, staleAfterHours: -1);

        Assert.False(result.IsSuccess);
        Assert.Equal("staleAfterHours cannot be negative", result.Error);
    }

    [Fact]
    public void SelectDue_NoItems_ReturnsEmpty()
    {
        var result = _sut.SelectItemsDueForSync([], Now, staleAfterHours: 6);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void SelectDue_LastSyncedAtReadBackAsUnspecified_ComparesCorrectlyAgainstUtcNow()
    {
        // LastSyncedAt is written from DateTime.UtcNow but read back from "timestamp without time zone" as Unspecified.
        // DateTime comparison uses ticks only, so the Kind mismatch must not shift the staleness cutoff.
        var converter = new UnspecifiedKindDateTimeConverter();
        DateTime ReadBack(DateTime utc) => (DateTime)converter.ConvertFromProvider(converter.ConvertToProvider(utc))!;
        var stale = ItemSyncedAt(1, ReadBack(Now.AddHours(-6).AddTicks(-1)));
        var fresh = ItemSyncedAt(2, ReadBack(Now.AddHours(-6)));
        Assert.Equal(DateTimeKind.Unspecified, stale.LastSyncedAt!.Value.Kind);

        var result = _sut.SelectItemsDueForSync([stale, fresh], Now, staleAfterHours: 6);

        Assert.Equal([1], result.Value!.Select(i => i.Id));
    }

    [Fact]
    public void SelectDue_NullItems_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.SelectItemsDueForSync(null!, Now, staleAfterHours: 6));
    }

    // ── IsAlreadyLinked: re-linking an institution that is already active ───

    private static PlaidItem ActiveItem(string institutionId, params PlaidAccount[] accounts) => new()
    {
        Id = 11,
        UserId = 5,
        InstitutionId = institutionId,
        IsActive = true,
        Accounts = accounts.ToList()
    };

    private static PlaidAccount Snapshot(string plaidAccountId, string name, string? mask) => new()
    {
        PlaidAccountId = plaidAccountId,
        Name = name,
        Mask = mask,
        AccountType = "depository"
    };

    private static PlaidAccountDto Incoming(string accountId, string name, string? mask) =>
        new(accountId, name, mask, "depository", "checking");

    [Fact]
    public void Should_ReturnTrue_When_IncomingAccountIdMatchesAnActiveSnapshot()
    {
        var active = ActiveItem("ins_3", Snapshot("acct-1", "Plaid Checking", "0000"));

        var result = _sut.IsAlreadyLinked("ins_3", [Incoming("acct-1", "Plaid Checking", "0000")], [active]);

        Assert.True(result);
    }

    [Fact]
    public void Should_ReturnTrue_When_SameInstitutionHasAccountWithSameMaskAndName()
    {
        // Production Plaid issues new account_ids for a new Item, so mask + name is the duplicate signal there.
        var active = ActiveItem("ins_3", Snapshot("acct-old", "Plaid Checking", "0000"));

        var result = _sut.IsAlreadyLinked("ins_3", [Incoming("acct-new", "Plaid Checking", "0000")], [active]);

        Assert.True(result);
    }

    [Fact]
    public void Should_ReturnTrue_When_MaskAndNameMatchIgnoringCase()
    {
        var active = ActiveItem("ins_3", Snapshot("acct-old", "Plaid Checking", "0000"));

        var result = _sut.IsAlreadyLinked("ins_3", [Incoming("acct-new", "PLAID CHECKING", "0000")], [active]);

        Assert.True(result);
    }

    [Fact]
    public void Should_ReturnFalse_When_SameInstitutionButDifferentAccounts()
    {
        // A second login at the same bank (e.g. personal and business) is legitimate.
        var active = ActiveItem("ins_3", Snapshot("acct-personal", "Personal Checking", "1111"));

        var result = _sut.IsAlreadyLinked("ins_3", [Incoming("acct-biz", "Business Checking", "2222")], [active]);

        Assert.False(result);
    }

    [Fact]
    public void Should_ReturnFalse_When_MaskAndNameMatchAtADifferentInstitution()
    {
        var active = ActiveItem("ins_3", Snapshot("acct-chase", "Checking", "0000"));

        var result = _sut.IsAlreadyLinked("ins_4", [Incoming("acct-wf", "Checking", "0000")], [active]);

        Assert.False(result);
    }

    [Fact]
    public void IsAlreadyLinked_SameAccountIdAtDifferentInstitution_ReturnsTrue()
    {
        // A Plaid account_id is globally unique, so a match is a duplicate even if institution metadata differs.
        var active = ActiveItem("ins_3", Snapshot("acct-1", "Plaid Checking", "0000"));

        var result = _sut.IsAlreadyLinked("ins_4", [Incoming("acct-1", "Everyday Checking", "9999")], [active]);

        Assert.True(result);
    }

    [Fact]
    public void IsAlreadyLinked_MatchingAccountOnlyOnInactiveItem_ReturnsFalse()
    {
        var inactive = ActiveItem("ins_3", Snapshot("acct-1", "Plaid Checking", "0000"));
        inactive.IsActive = false;

        var result = _sut.IsAlreadyLinked("ins_3", [Incoming("acct-1", "Plaid Checking", "0000")], [inactive]);

        Assert.False(result);
    }

    [Fact]
    public void Should_ReturnFalse_When_MasksAreNullEvenIfNamesMatch()
    {
        // Without a mask, a name like "Checking" is too weak a signal to block a link.
        var active = ActiveItem("ins_3", Snapshot("acct-old", "Checking", null));

        var result = _sut.IsAlreadyLinked("ins_3", [Incoming("acct-new", "Checking", null)], [active]);

        Assert.False(result);
    }

    [Fact]
    public void Should_ReturnFalse_When_UserHasNoActiveItems()
    {
        var result = _sut.IsAlreadyLinked("ins_3", [Incoming("acct-1", "Plaid Checking", "0000")], []);

        Assert.False(result);
    }

    [Fact]
    public void Should_ReturnFalse_When_IncomingItemHasNoAccounts()
    {
        var active = ActiveItem("ins_3", Snapshot("acct-1", "Plaid Checking", "0000"));

        var result = _sut.IsAlreadyLinked("ins_3", [], [active]);

        Assert.False(result);
    }

    [Fact]
    public void Should_StillMatchByAccountId_When_InstitutionIdIsNull()
    {
        var active = ActiveItem("ins_3", Snapshot("acct-1", "Plaid Checking", "0000"));

        var result = _sut.IsAlreadyLinked(null!, [Incoming("acct-1", "Plaid Checking", "0000")], [active]);

        Assert.True(result);
    }

    [Fact]
    public void Should_Throw_When_IncomingAccountsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.IsAlreadyLinked("ins_3", null!, []));
    }

    [Fact]
    public void Should_Throw_When_ActiveItemsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.IsAlreadyLinked("ins_3", [], null!));
    }
}
