using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;
using BudgetTracker.Server.Managers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// A brand-new user's first bank, end to end on real PostgreSQL: only Plaid's HTTP calls are faked. The first sync
/// creates starter categories and files its rows; Plaid's later history sync files against them without adding more;
/// the money map then has a line per category plus one for the unsorted transfers the budget guide drafts from,
/// averaged over the weeks the history covers. A move into investing is left out of spending.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Postgres")]
public sealed class FirstSyncBudgetPostgresTests(PostgresFixture fixture)
{
    private const string AccountId = "first-sync-chk";

    // Plaid transaction ids are unique across the shared test database, so each run gets its own prefix.
    private readonly string _run = Guid.NewGuid().ToString("N")[..8];
    private readonly RecordingLogger _log = new();

    // One key ring for the run, so the token stored at link time decrypts at sync time.
    private readonly EphemeralDataProtectionProvider _keyRing = new();

    /// <summary>The manager logs and swallows a failed sync; this keeps the exception for the assert message.</summary>
    private sealed class RecordingLogger : Microsoft.Extensions.Logging.ILogger<PlaidManager>
    {
        public List<string> Errors { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) Errors.Add(exception.ToString());
        }
    }

    [PostgresFact]
    public async Task Should_FileANewUsersFirstSyncIntoStarterCategories_AndFeedTheMoneyMap()
    {
        var user = await fixture.UserAsync();
        var today = DateTime.UtcNow.Date;
        await LinkCheckingAsync(user.Id);

        var plaid = new Mock<IPlaidAccessor>();
        plaid.SetupSequence(p => p.SyncTransactionsAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            // Plaid's first answer: about the last 30 days.
            .ReturnsAsync(Sync("c1",
                Row("t1", today.AddDays(-3), -2400m, "Employer Payroll", "INCOME"),
                Row("t2", today.AddDays(-5), 1850m, "Mortgage Servicer", "LOAN_PAYMENTS"),
                Row("t3", today.AddDays(-6), 182.40m, "H-E-B", "FOOD_AND_DRINK"),
                Row("t4", today.AddDays(-8), 140m, "City Utilities", "RENT_AND_UTILITIES"),
                Row("t5", today.AddDays(-9), 60m, "Zelle payment to a friend", "TRANSFER_OUT"),
                Row("t6", today.AddDays(-10), 45m, "Amazon", "GENERAL_MERCHANDISE"),
                Row("t11", today.AddDays(-12), 500m, "Webull", "TRANSFER_OUT", "TRANSFER_OUT_INVESTMENT_AND_RETIREMENT_FUNDS")))
            // HISTORICAL_UPDATE: older months arrive later, after the categories exist.
            .ReturnsAsync(Sync("c2",
                Row("t7", today.AddDays(-40), 210.10m, "H-E-B", "FOOD_AND_DRINK"),
                Row("t8", today.AddDays(-41), -2400m, "Employer Payroll", "INCOME"),
                Row("t9", today.AddDays(-45), 75m, "Venmo", "TRANSFER_OUT"),
                Row("t10", today.AddDays(-47), 38m, "Shell", "TRANSPORTATION")));

        await SyncOkAsync(plaid.Object, user.Id);
        var afterFirst = await CategoriesAsync(user.Id);
        Assert.Equal(
            new[] { "FOOD_AND_DRINK", "GENERAL_MERCHANDISE", "INCOME", "LOAN_PAYMENTS", "RENT_AND_UTILITIES" },
            afterFirst.Select(c => c.PlaidCategoryPrimary).Order());

        await SyncOkAsync(plaid.Object, user.Id);
        var categories = await CategoriesAsync(user.Id);
        // Transportation is new in the history; the rest are not created twice.
        Assert.Equal(afterFirst.Count + 1, categories.Count);
        var byPrimary = categories.ToDictionary(c => c.PlaidCategoryPrimary!, c => c);
        Assert.Equal("Income", byPrimary["INCOME"].CategoryType);

        await using var check = fixture.CreateContext();
        var rows = await check.Transactions.AsNoTracking()
            .Where(t => t.Account.UserId == user.Id)
            .ToDictionaryAsync(t => t.PlaidTransactionId![(_run.Length + 1)..]);
        Assert.Equal(11, rows.Count);
        Assert.Equal(byPrimary["FOOD_AND_DRINK"].Id, rows["t7"].CategoryId);
        Assert.Equal(byPrimary["INCOME"].Id, rows["t8"].CategoryId);
        Assert.Equal("Income", rows["t8"].TransactionType);
        // Venmo and Zelle say nothing about what the money was for, so they stay unsorted.
        Assert.Null(rows["t5"].CategoryId);
        Assert.Null(rows["t9"].CategoryId);
        // Money moved to investing is still the user's money: unsorted, but not spending.
        Assert.Null(rows["t11"].CategoryId);
        Assert.Equal("Adjustment", rows["t11"].TransactionType);

        await using var mapContext = fixture.CreateContext();
        var map = await new MoneyMapManager(
                new MoneyMapEngine(), new TransactionAccessor(mapContext), new BudgetPlanAccessor(mapContext), new CategoryAccessor(mapContext))
            .GetAsync(user.Id, 12, today);
        Assert.True(map.IsSuccess);
        Assert.Null(map.Value!.PlanName);
        Assert.Contains(map.Value.Lines, l => l.CategoryId == byPrimary["LOAN_PAYMENTS"].Id);
        // The history covers 48 days (the oldest row is 47 days back), so the averages are over that, not 12 months.
        var months = 48 / (365.25m / 12);
        Assert.Equal(months, map.Value.MonthsAveraged);
        var unsorted = Assert.Single(map.Value.Lines, l => l.CategoryId is null);
        Assert.Equal(Math.Round(135m / months, 2, MidpointRounding.AwayFromZero), unsorted.ActualMonthly);
        Assert.Equal(Math.Round(4800m / months, 2, MidpointRounding.AwayFromZero), map.Value.ActualIncomeMonthly);
    }

    private async Task LinkCheckingAsync(int userId)
    {
        await using var context = fixture.CreateContext();
        context.Accounts.Add(new Account { UserId = userId, Name = "Chase - Checking (••1234)", AccountType = "depository" });
        await context.SaveChangesAsync();
        await new PlaidItemAccessor(context, _keyRing).AddAsync(
            userId, "access-production-test", $"item-{Guid.NewGuid():N}", "ins_3", "Chase", null,
            [new PlaidAccount { PlaidAccountId = AccountId, Name = "Checking", Mask = "1234", AccountType = "depository" }]);
    }

    /// <summary>One sync through the real manager and accessors, on a fresh context as each request gets.</summary>
    private async Task<Domain.Common.Result<PlaidSyncSummary>> SyncAsync(IPlaidAccessor plaid, int userId)
    {
        await using var context = fixture.CreateContext();
        var manager = new PlaidManager(
            plaid,
            new PlaidItemAccessor(context, _keyRing),
            new TransactionAccessor(context),
            new AccountAccessor(context),
            new CategoryAccessor(context),
            new PlaidEngine(),
            Mock.Of<IPlaidWebhookEngine>(),
            new DataVersionAccessor(context, NullLogger<DataVersionAccessor>.Instance),
            new BudgetPlanAccessor(context),
            new BudgetPlanEngine(),
            Mock.Of<ISandboxSeedEngine>(),
            new CategoryRuleAccessor(context),
            new CategoryRuleEngine(),
            Options.Create(new PlaidOptions()),
            _log);
        return await manager.SyncAsync(userId);
    }

    private async Task SyncOkAsync(IPlaidAccessor plaid, int userId)
    {
        var result = await SyncAsync(plaid, userId);
        Assert.True(result.IsSuccess, $"{result.Error} {string.Join(" | ", _log.Errors)}");
    }

    private async Task<List<Category>> CategoriesAsync(int userId)
    {
        await using var context = fixture.CreateContext();
        return await context.Categories.AsNoTracking().Where(c => c.UserId == userId).ToListAsync();
    }

    private static PlaidSyncResult Sync(string cursor, params PlaidTransactionDto[] added) =>
        new(Added: added, Modified: [], RemovedTransactionIds: [], NextCursor: cursor);

    /// <summary>A Plaid row: positive amounts are money out, negative money in.</summary>
    private PlaidTransactionDto Row(string id, DateTime date, decimal amount, string payee, string primary, string? detailed = null) =>
        new(TransactionId: $"{_run}-{id}", AccountId: AccountId, Amount: amount, Date: date,
            MerchantName: payee, Name: payee, Pending: false, PersonalFinanceCategoryPrimary: primary,
            PersonalFinanceCategoryDetailed: detailed);
}
