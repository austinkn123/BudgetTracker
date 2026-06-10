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
}
