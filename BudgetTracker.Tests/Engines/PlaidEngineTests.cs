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
    public void ResolveCategoryId_UsersPickForThePayee_BeatsThePlaidMapping()
    {
        var categories = new[] { new Category { Id = 8, PlaidCategoryPrimary = "FOOD_AND_DRINK" } };
        var categoryByPayee = new Dictionary<string, int> { ["Starbucks"] = 12 };

        Assert.Equal(12, _sut.ResolveCategoryId(BuildPlaidDebit(), categories, categoryByPayee));
    }

    [Fact]
    public void ResolveCategoryId_PayeeFallsBackToNameWithoutMerchant()
    {
        // The stored Payee is merchant_name ?? name, so the lookup has to key on the same value.
        var dto = BuildPlaidDebit() with { MerchantName = null, PersonalFinanceCategoryPrimary = null };
        var categoryByPayee = new Dictionary<string, int> { ["STARBUCKS #1234"] = 12 };

        Assert.Equal(12, _sut.ResolveCategoryId(dto, Array.Empty<Category>(), categoryByPayee));
    }

    [Fact]
    public void ResolveCategoryId_UnknownPayee_FallsBackToThePlaidMapping()
    {
        var categories = new[] { new Category { Id = 8, PlaidCategoryPrimary = "FOOD_AND_DRINK" } };
        var categoryByPayee = new Dictionary<string, int> { ["Amazon"] = 12 };

        Assert.Equal(8, _sut.ResolveCategoryId(BuildPlaidDebit(), categories, categoryByPayee));
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

    // ── Boundary: zero amount ───────────────────────────────────────────────

    [Fact]
    public void Map_ZeroAmount_MapsToExpenseAndPreservesZero()
    {
        // Map itself doesn't crash on zero; sync filters $0 rows out via IsImportable before mapping,
        // since CK_Transactions_NonZeroAmount would reject them.
        var dto = BuildPlaidDebit(amount: 0m);

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7);

        Assert.Equal(0m, result.Amount);
    }

    // ── IsImportable: Plaid does send $0 rows (seen on a real Bank of America link) ─

    [Fact]
    public void IsImportable_ZeroAmount_ReturnsFalse()
    {
        Assert.False(_sut.IsImportable(BuildPlaidDebit(amount: 0m)));
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(-0.01)]
    public void IsImportable_NonZeroAmount_ReturnsTrue(double amount)
    {
        Assert.True(_sut.IsImportable(BuildPlaidDebit(amount: (decimal)amount)));
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

    // ── ClassifyTransactionType: only Plaid INCOME is income unless the category says otherwise ─

    [Theory]
    [InlineData("INCOME", null, "Income")]
    [InlineData("TRANSFER_IN", null, "Expense")]        // money from a friend nets against spending
    [InlineData("GENERAL_MERCHANDISE", null, "Expense")] // a refund
    [InlineData(null, null, "Expense")]                  // manual row, no Plaid category
    [InlineData("INCOME", "Both", "Income")]             // Both defers to the automatic rule
    [InlineData("TRANSFER_IN", "Both", "Expense")]
    public void Classify_WithoutAnOverridingCategory_OnlyPlaidIncomeIsIncome(string? plaidCategory, string? categoryType, string expected)
    {
        Assert.Equal(expected, _sut.ClassifyTransactionType(plaidCategory, categoryType));
    }

    [Theory]
    [InlineData("TRANSFER_IN", "Income", "Income")]   // e.g. a side gig paid by Zelle
    [InlineData("INCOME", "Expense", "Expense")]      // e.g. an interest line the user files as spending
    public void Classify_IncomeOrExpenseCategory_OverridesPlaid(string plaidCategory, string categoryType, string expected)
    {
        Assert.Equal(expected, _sut.ClassifyTransactionType(plaidCategory, categoryType));
    }

    [Theory]
    [InlineData("Income")]
    [InlineData("Transfer")]
    public void Classify_PairedTransferRow_KeepsItsType(string? categoryType)
    {
        Assert.Equal("Transfer", _sut.ClassifyTransactionType("TRANSFER_OUT", categoryType, "Transfer"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Expense")]
    [InlineData("Adjustment")]
    public void Classify_TransferCategory_TakesTheRowOutOfSpending(string? currentType)
    {
        // A Webull deposit or a card payment whose other side is not linked: money moved, nothing was spent.
        Assert.Equal("Adjustment", _sut.ClassifyTransactionType("TRANSFER_OUT", "Transfer", currentType));
    }

    [Fact]
    public void Classify_AdjustmentRow_FollowsTheCategoryItMovesTo()
    {
        Assert.Equal("Expense", _sut.ClassifyTransactionType("TRANSFER_OUT", "Expense", "Adjustment"));
    }

    [Fact]
    public void Classify_IncomeOrExpenseRow_IsReDerived()
    {
        Assert.Equal("Income", _sut.ClassifyTransactionType("TRANSFER_IN", "Income", currentType: "Expense"));
    }

    [Fact]
    public void Map_Refund_IsAnExpenseWithPositiveAmount()
    {
        var dto = BuildPlaidDebit(amount: -25m) with { PersonalFinanceCategoryPrimary = "GENERAL_MERCHANDISE" };

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7);

        Assert.Equal(25m, result.Amount);
        Assert.Equal("Expense", result.TransactionType);
    }

    [Fact]
    public void Map_IncomeCategoryType_OverridesPlaidCategory()
    {
        var dto = BuildPlaidDebit(amount: -300m) with { PersonalFinanceCategoryPrimary = "TRANSFER_IN" };

        var result = _sut.MapToBudgetTrackerTransaction(dto, accountId: 7, categoryId: 4, categoryType: "Income");

        Assert.Equal("Income", result.TransactionType);
    }

    // ── ReconcileOwnAccountTransfers ───────────────────────────────────────

    private static readonly DateTime TransferDay = new(2026, 9, 10);

    // BudgetTracker account ids: chk 10, sav 20, card 30, loan 40, brokerage 50.
    private static readonly PlaidAccount[] Linked =
    [
        new() { PlaidAccountId = "chk", AccountType = "depository" },
        new() { PlaidAccountId = "sav", AccountType = "depository" },
        new() { PlaidAccountId = "card", AccountType = "credit" },
        new() { PlaidAccountId = "loan", AccountType = "loan" },
        new() { PlaidAccountId = "brokerage", AccountType = "investment" }
    ];

    private static Transaction Leg(
        int id, decimal amount, string plaidAccountId, int accountId, int dayOffset = 0,
        string plaidCategory = "TRANSFER_OUT", bool pending = false, string type = "Expense",
        int? transferAccountId = null, int? categoryId = null) => new()
    {
        Id = id,
        AccountId = accountId,
        PlaidAccountId = plaidAccountId,
        Amount = amount,
        OccurredAt = TransferDay.AddDays(dayOffset),
        PlaidCategoryPrimary = plaidCategory,
        IsPending = pending,
        TransactionType = type,
        TransferAccountId = transferAccountId,
        CategoryId = categoryId
    };

    private IReadOnlyList<TransferUpdate> Reconcile(IEnumerable<Transaction> rows, params Category[] categories) =>
        _sut.ReconcileOwnAccountTransfers(rows, Linked, categories);

    /// <summary>(row, counterpart account) for every leg marked Transfer, by row id.</summary>
    private static List<(int, int?)> Pairs(IReadOnlyList<TransferUpdate> updates) =>
        updates.Where(u => u.TransactionType == "Transfer").OrderBy(u => u.TransactionId).Select(u => (u.TransactionId, u.TransferAccountId)).ToList();

    [Fact]
    public void Pair_OppositeAmountsAcrossAccounts_MarksBothLegsAsTransfersToTheOtherAccount()
    {
        var updates = Reconcile(
        [
            Leg(1, -500m, "chk", accountId: 10),
            Leg(2, 500m, "sav", accountId: 20, dayOffset: 1, plaidCategory: "TRANSFER_IN")
        ]);

        Assert.Equal([new TransferUpdate(1, "Transfer", 20), new TransferUpdate(2, "Transfer", 10)], updates.OrderBy(u => u.TransactionId));
    }

    [Fact]
    public void Pair_CardPaymentCategorisedAsLoanPayment_Pairs()
    {
        var updates = Reconcile(
        [
            Leg(1, -900m, "chk", accountId: 10, plaidCategory: "LOAN_PAYMENTS"),
            Leg(2, 900m, "card", accountId: 30, dayOffset: 2, plaidCategory: "LOAN_PAYMENTS")
        ]);

        Assert.Equal([(1, 30), (2, 10)], Pairs(updates));
    }

    [Theory]
    [InlineData("loan")]        // a payment to a linked loan is money out, as the balance chart sees it
    [InlineData("brokerage")]   // so is a move into investments
    [InlineData("unlinked")]
    public void Pair_LegOnANonCashAccount_StaysMoneyInOrOut(string plaidAccountId)
    {
        var updates = Reconcile(
        [
            Leg(1, -900m, "chk", 10, plaidCategory: "LOAN_PAYMENTS"),
            Leg(2, 900m, plaidAccountId, 40, dayOffset: 1, plaidCategory: "LOAN_PAYMENTS")
        ]);

        Assert.Empty(updates);
    }

    [Theory]
    [InlineData(5, 2)]   // boundary: five days apart still pairs
    [InlineData(-5, 2)]
    [InlineData(6, 0)]   // a day further is a different event
    [InlineData(-6, 0)]
    public void Pair_DatesMoreThanFiveDaysApart_DoNotPair(int dayOffset, int expectedLegs)
    {
        var updates = Reconcile(
        [
            Leg(1, -500m, "chk", 10),
            Leg(2, 500m, "sav", 20, dayOffset, "TRANSFER_IN")
        ]);

        Assert.Equal(expectedLegs, Pairs(updates).Count);
    }

    [Fact]
    public void Pair_SamePlaidAccount_IsRejected()
    {
        // A reversal on one account is not a transfer between two.
        Assert.Empty(Reconcile([Leg(1, -500m, "chk", 10), Leg(2, 500m, "chk", 10, 1, "TRANSFER_IN")]));
    }

    [Fact]
    public void Pair_NonTransferCategory_IsRejected()
    {
        // Payroll that happens to equal an outgoing transfer is still income.
        Assert.Empty(Reconcile([Leg(1, -500m, "chk", 10), Leg(2, 500m, "sav", 20, 1, "INCOME")]));
    }

    [Fact]
    public void Pair_AmountsNotExactlyOpposite_DoNotPair()
    {
        Assert.Empty(Reconcile([Leg(1, -500m, "chk", 10), Leg(2, 499.99m, "sav", 20, 0, "TRANSFER_IN")]));
    }

    [Fact]
    public void Pair_PendingAndAdjustmentRows_AreLeftAlone()
    {
        var updates = Reconcile(
        [
            Leg(1, -500m, "chk", 10, pending: true),
            Leg(2, 500m, "sav", 20, 0, "TRANSFER_IN"),
            Leg(3, -70m, "chk", 10, type: "Adjustment"),
            Leg(4, 70m, "sav", 20, 0, "TRANSFER_IN")
        ]);

        Assert.Empty(updates);
    }

    [Fact]
    public void Pair_MaximisesThePairCount()
    {
        // Closest-first would pair outflow 2 (day 3) with inflow 3 (day 2) and strand the other two legs;
        // 1↔3 and 2↔4 are both inside the window.
        var updates = Reconcile(
        [
            Leg(1, -200m, "chk", 10, dayOffset: 0),
            Leg(2, -200m, "chk", 10, dayOffset: 3),
            Leg(3, 200m, "sav", 20, dayOffset: 2, plaidCategory: "TRANSFER_IN"),
            Leg(4, 200m, "sav", 20, dayOffset: 6, plaidCategory: "TRANSFER_IN")
        ]);

        Assert.Equal([(1, 20), (2, 20), (3, 10), (4, 10)], Pairs(updates));
    }

    [Fact]
    public void Pair_EachRowIsUsedAtMostOnce()
    {
        // Two outflows, one inflow: only one pair is possible.
        var updates = Reconcile(
        [
            Leg(1, -200m, "chk", 10),
            Leg(2, -200m, "chk", 10, dayOffset: 2),
            Leg(3, 200m, "sav", 20, dayOffset: 1, plaidCategory: "TRANSFER_IN")
        ]);

        Assert.Equal(2, updates.Count);
        Assert.Single(updates, u => u.TransactionId == 3);
    }

    [Fact]
    public void Pair_Tie_IsDeterministicRegardlessOfInputOrder()
    {
        Transaction[] rows =
        [
            Leg(5, -200m, "chk", 10, dayOffset: -1),
            Leg(3, -200m, "chk", 10, dayOffset: 1),
            Leg(4, 200m, "sav", 20, dayOffset: 0, plaidCategory: "TRANSFER_IN")
        ];

        var forward = Reconcile(rows);
        var reversed = Reconcile(rows.Reverse());

        Assert.Equal(forward.OrderBy(u => u.TransactionId), reversed.OrderBy(u => u.TransactionId));
        Assert.Contains(forward, u => u.TransactionId == 5);   // the earlier outflow takes the inflow
    }

    [Fact]
    public void Pair_NoCandidates_ReturnsEmpty() => Assert.Empty(Reconcile([]));

    [Fact]
    public void Pair_TwoEqualTransfersOnTheSameDay_BothPair()
    {
        var updates = Reconcile(
        [
            Leg(1, -100m, "chk", 10),
            Leg(2, -100m, "chk", 10),
            Leg(3, 100m, "sav", 20, plaidCategory: "TRANSFER_IN"),
            Leg(4, 100m, "sav", 20, plaidCategory: "TRANSFER_IN")
        ]);

        Assert.Equal([1, 2, 3, 4], Pairs(updates).Select(p => p.Item1));
    }

    [Fact]
    public void Pair_InflowWhoseClosestOutflowIsTaken_StillPairs()
    {
        var updates = Reconcile(
        [
            Leg(1, -300m, "chk", 10),
            Leg(2, -300m, "chk", 10, dayOffset: 4),
            Leg(3, 300m, "sav", 20, plaidCategory: "TRANSFER_IN"),
            Leg(4, 300m, "sav", 20, dayOffset: 1, plaidCategory: "TRANSFER_IN")
        ]);

        Assert.Equal([(1, 20), (2, 20), (3, 10), (4, 10)], Pairs(updates));
    }

    [Fact]
    public void Pair_PendingLegAndItsPostedCopy_OnlyThePostedCopyPairs()
    {
        // Plaid posts a pending row under a new id and removes the pending one later; until then both exist.
        var updates = Reconcile(
        [
            Leg(1, -250m, "chk", 10, pending: true),
            Leg(2, -250m, "chk", 10, dayOffset: 1),
            Leg(3, 250m, "sav", 20, plaidCategory: "TRANSFER_IN")
        ]);

        Assert.Equal([(2, 20), (3, 10)], Pairs(updates));
    }

    [Fact]
    public void Pair_CardPaymentToAnUnlinkedCard_StaysUnpaired()
    {
        // No matching leg on a linked card, so the payment is real money out of checking.
        Assert.Empty(Reconcile(
        [
            Leg(1, -800m, "chk", 10, plaidCategory: "LOAN_PAYMENTS"),
            Leg(2, -800m, "chk", 10, plaidCategory: "LOAN_PAYMENTS")
        ]));
    }

    // ── Re-pairing from scratch: only changes are written, and lost pairs revert ─

    [Fact]
    public void Reconcile_AlreadyPairedLegs_NeedNoUpdate()
    {
        var updates = Reconcile(
        [
            Leg(1, -500m, "chk", 10, type: "Transfer", transferAccountId: 20),
            Leg(2, 500m, "sav", 20, 1, "TRANSFER_IN", type: "Transfer", transferAccountId: 10)
        ]);

        Assert.Empty(updates);
    }

    [Fact]
    public void Reconcile_WritesOnlyRowsWhoseResultChanged()
    {
        // Leg 1 pointed at the wrong account; leg 2 is already right.
        var updates = Reconcile(
        [
            Leg(1, -500m, "chk", 10, type: "Transfer", transferAccountId: 30),
            Leg(2, 500m, "sav", 20, 1, "TRANSFER_IN", type: "Transfer", transferAccountId: 10)
        ]);

        Assert.Equal([new TransferUpdate(1, "Transfer", 20)], updates);
    }

    [Fact]
    public void Reconcile_PartnerDeletedByPlaid_SurvivingLegRevertsToItsClassifiedType()
    {
        var updates = Reconcile([Leg(2, 500m, "sav", 20, 1, "TRANSFER_IN", type: "Transfer", transferAccountId: 10)]);

        Assert.Equal([new TransferUpdate(2, "Expense", null)], updates);
    }

    [Fact]
    public void Reconcile_SurvivingLegInAnIncomeCategory_RevertsToIncome()
    {
        var updates = Reconcile(
            [Leg(2, 500m, "sav", 20, 1, "TRANSFER_IN", type: "Transfer", transferAccountId: 10, categoryId: 9)],
            new Category { Id = 9, Name = "Side gig", CategoryType = "Income" });

        Assert.Equal([new TransferUpdate(2, "Income", null)], updates);
    }

    [Fact]
    public void Reconcile_PairWithANonCashAccount_BothLegsRevert()
    {
        // Paired before the cash-accounts-only rule; a move into investments is money out again.
        var updates = Reconcile(
        [
            Leg(1, -500m, "chk", 10, type: "Transfer", transferAccountId: 50),
            Leg(2, 500m, "brokerage", 50, 1, "TRANSFER_IN", type: "Transfer", transferAccountId: 10)
        ]);

        Assert.Equal([new TransferUpdate(1, "Expense", null), new TransferUpdate(2, "Expense", null)], updates.OrderBy(u => u.TransactionId));
    }
}
