using System.Text.Json;
using System.Text.Json.Serialization;
using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.CashFlow;
using BudgetTracker.Domain.Webull;
using BudgetTracker.Server.Managers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Tests.Managers;

/// <summary>
/// AC 17: Webull is read-only and separate. Budget analysis and cash flow, run through their real managers, accessors and
/// engines, return byte-identical JSON with and without Webull rows, even when a Webull account id collides with a
/// Plaid account id, and with saved Webull trade history.
/// </summary>
public class WebullSeparationTests : IDisposable
{
    private const int UserId = 5;
    private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly BudgetTrackerDbContext _context;
    private readonly EphemeralDataProtectionProvider _keyRing = new();

    public WebullSeparationTests()
    {
        _context = new BudgetTrackerDbContext(new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    public void Dispose() => _context.Dispose();

    private async Task SeedBudgetDataAsync(DateTime month)
    {
        _context.Accounts.Add(new Account { Id = 1, UserId = UserId, Name = "Checking", AccountType = "Checking" });
        _context.Categories.Add(new Category { Id = 1, UserId = UserId, Name = "Groceries", CategoryType = "Expense" });
        _context.Transactions.AddRange(
            new Transaction { AccountId = 1, CategoryId = 1, TransactionType = "Expense", Amount = -82.40m, OccurredAt = month.AddDays(2), PlaidAccountId = "chk" },
            new Transaction { AccountId = 1, TransactionType = "Income", Amount = 2500m, OccurredAt = month.AddDays(1), PlaidAccountId = "chk" },
            new Transaction { AccountId = 1, CategoryId = 1, TransactionType = "Expense", Amount = -40m, OccurredAt = month.AddMonths(-1).AddDays(5), PlaidAccountId = "chk" });
        _context.BudgetPlans.Add(new BudgetPlan
        {
            UserId = UserId,
            Name = "Plan",
            PlanMonth = month,
            NetIncomeMonthly = 2500m,
            IsActive = true,
            Entries = [new BudgetPlanEntry { CategoryId = 1, LineType = "Expense", Bucket = "Needs", Cadence = "Monthly", Amount = 300m, MonthlyEquivalent = 300m }]
        });
        _context.PlaidItems.Add(new PlaidItem
        {
            UserId = UserId,
            PlaidItemId = "item-1",
            InstitutionId = "ins_1",
            InstitutionName = "Bank",
            AccessTokenEncrypted = "x",
            Accounts = [new PlaidAccount { PlaidAccountId = "chk", Name = "Checking", AccountType = "depository", CurrentBalance = 1000m }]
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private async Task<string> BudgetOutputsAsync(DateTime month)
    {
        var to = month.AddMonths(1).AddDays(-1);
        var analysis = await new BudgetAnalysisManager(new BudgetAnalysisEngine(), new TransactionAccessor(_context), new BudgetPlanAccessor(_context))
            .GetAnalysisAsync(UserId, month.AddMonths(-1), to, 6);
        var cashFlow = await new CashFlowManager(new CashFlowEngine(), new TransactionAccessor(_context), new PlaidItemAccessor(_context, _keyRing))
            .GetReportAsync(UserId, new CashFlowQuery(month.AddMonths(-1), to, CashFlowGrouping.Month));
        var transactions = await new TransactionManager(new TransactionAccessor(_context), new CategoryAccessor(_context), new PlaidEngine(), new TransactionEngine())
            .GetPageAsync(UserId, new TransactionFilter(), cursor: null, limit: 200);

        Assert.True(analysis.IsSuccess, analysis.Error);
        Assert.True(cashFlow.IsSuccess, cashFlow.Error);
        Assert.True(transactions.IsSuccess, transactions.Error);
        return JsonSerializer.Serialize(new { analysis = analysis.Value, cashFlow = cashFlow.Value, transactions = transactions.Value }, ApiJson);
    }

    [Fact]
    public async Task BudgetAnalysisAndCashFlow_AreIdentical_WithAndWithoutWebullRows()
    {
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        await SeedBudgetDataAsync(month);
        var without = await BudgetOutputsAsync(month);

        // Two days of holdings and daily values, with ids and symbols that collide with budget data ("chk" is the Plaid
        // account id, "Groceries" the category name).
        var webull = new WebullConnectionAccessor(_context, _keyRing);
        await webull.SaveTokenAsync(UserId, "tok", WebullTokenStatus.Normal, DateTime.UtcNow);
        IReadOnlyList<WebullPosition> positions =
        [
            new("chk", "EQUITY", 3m, 10m, 9m, 3m, "USD"),
            new("Groceries", "EQUITY", 1m, 82.4m, 80m, 2.4m, "USD"),
            new("AAPL", "EQUITY", 5m, 150m, 120m, 150m, "USD")
        ];
        IReadOnlyList<WebullAccountPortfolio> portfolio =
        [
            new(new WebullBalance("chk", "Individual Cash", "CASH", 987654.32m, 1234m, "USD"), 986420.32m, -82.4m, 2500m, positions),
            new(new WebullBalance("w-2", null, "MARGIN", 5m, 0m, "USD"), 5m, 1m, -1m, [])
        ];
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await webull.SavePortfolioAsync(UserId, portfolio, DateTime.UtcNow.AddDays(-1), today.AddDays(-1));
        await webull.SavePortfolioAsync(UserId, portfolio, DateTime.UtcNow, today);
        _context.ChangeTracker.Clear();

        // Trade history in this month, filled and open, for the colliding account id and symbols.
        IReadOnlyList<WebullOrder> orders =
        [
            new("chk", "1", "Groceries", "EQUITY", "BUY", "FILLED", 2m, 82.4m, month.AddDays(1), month.AddDays(1)),
            new("chk", "2", "Groceries", "EQUITY", "SELL", "FILLED", 1m, 2500m, month.AddDays(2), month.AddDays(2)),
            new("chk", "3", "chk", "CRYPTO", "BUY", "PENDING", null, null, month.AddDays(3), null)
        ];
        var sync = new WebullOrderSync("chk", month, DateTime.UtcNow, new WebullOrderWindow(month.AddYears(-1), month, "pk=="), null);
        await new WebullOrderAccessor(_context).SavePageAsync(UserId, sync, orders);
        _context.ChangeTracker.Clear();
        var with = await BudgetOutputsAsync(month);

        Assert.Contains("2500", without); // the reports really carry the seeded budget data
        Assert.Contains("-82.4", without);
        Assert.Equal(3, await _context.WebullPositions.CountAsync());
        Assert.Equal(4, await _context.WebullDailyValues.CountAsync());
        Assert.Equal(3, await _context.WebullOrders.CountAsync());
        Assert.Equal(1, await _context.WebullOrderSyncs.CountAsync());
        Assert.Equal(without, with);
    }
}
