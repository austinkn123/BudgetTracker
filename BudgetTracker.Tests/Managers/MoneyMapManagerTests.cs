using System.Text.Json;
using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.MoneyMap;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class MoneyMapManagerTests
{
    private const int UserId = 5;
    private readonly Mock<IMoneyMapEngine> _engine = new(MockBehavior.Strict);
    private readonly Mock<ITransactionAccessor> _txnAccessor = new(MockBehavior.Strict);
    private readonly Mock<IBudgetPlanAccessor> _planAccessor = new(MockBehavior.Strict);
    private readonly Mock<ICategoryAccessor> _categoryAccessor = new(MockBehavior.Strict);

    private MoneyMapManager BuildSut(IMoneyMapEngine? engine = null) =>
        new(engine ?? _engine.Object, _txnAccessor.Object, _planAccessor.Object, _categoryAccessor.Object);

    private static DateTime Today => DateTime.UtcNow.Date;

    [Fact]
    public async Task Should_FetchOnlyTheHistoryTheEngineDescribes_AndHandItWithPlansAndCategoriesToTheEngine()
    {
        BudgetPlan[] plans = [new() { Id = 1, IsActive = true }];
        Transaction[] rows = [new() { Id = 2 }];
        Category[] categories = [new() { Id = 3 }];
        var from = Today.AddMonths(-12);
        var map = new MoneyMap("Plan", Today, Today, 6, 0m, 0m, [], []);
        _planAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(plans);
        _engine.Setup(e => e.DescribeHistory(plans, Today, 6)).Returns(Result<MoneyMapHistory>.Success(new MoneyMapHistory(from, [], 6)));
        var bounded = new TransactionFilter(From: from, To: Today);
        _txnAccessor.Setup(a => a.GetFilteredAsync(UserId, bounded)).ReturnsAsync(rows);
        _categoryAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(categories);
        _engine.Setup(e => e.Build(plans, It.Is<IReadOnlyList<Transaction>>(t => t.Single().Id == 2), categories, Today, 6))
            .Returns(Result<MoneyMap>.Success(map));

        var result = await BuildSut().GetAsync(UserId, 6);

        Assert.Same(map, result.Value);
    }

    [Fact]
    public async Task Should_ReturnTheEnginesError_When_MonthsIsOutOfRange_WithoutReadingTheLedger()
    {
        _planAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);
        _engine.Setup(e => e.DescribeHistory(It.IsAny<IEnumerable<BudgetPlan>>(), Today, 99))
            .Returns(Result<MoneyMapHistory>.Failure("months must be between 1 and 24"));

        var result = await BuildSut().GetAsync(UserId, 99);

        Assert.False(result.IsSuccess);
        Assert.Equal("months must be between 1 and 24", result.Error);
        _txnAccessor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Should_AddTheNewestQualifyingPaychecks_FromBeforeTheBoundedRange()
    {
        var from = Today.AddMonths(-12);
        var latest = new Transaction { Id = 50, Amount = 2000m, OccurredAt = Today.AddYears(-2) };
        var older = new Transaction { Id = 49, Amount = 2000m, OccurredAt = Today.AddYears(-2).AddDays(-14) };
        _planAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);
        _engine.Setup(e => e.DescribeHistory(It.IsAny<IEnumerable<BudgetPlan>>(), Today, 12))
            .Returns(Result<MoneyMapHistory>.Success(new MoneyMapHistory(from, [1], 6)));
        _engine.Setup(e => e.PaycheckFloor(2000m)).Returns(1000m);
        _txnAccessor.Setup(a => a.GetFilteredAsync(UserId, It.IsAny<TransactionFilter>())).ReturnsAsync([new Transaction { Id = 2 }]);
        _txnAccessor.Setup(a => a.GetNewestIncomeAsync(UserId, It.Is<IReadOnlyCollection<int>>(c => c.Single() == 1), Today, null, 1))
            .ReturnsAsync([latest]);
        _txnAccessor.Setup(a => a.GetNewestIncomeAsync(UserId, It.Is<IReadOnlyCollection<int>>(c => c.Single() == 1), Today, 1000m, 6))
            .ReturnsAsync([latest, older]);
        _categoryAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync([]);
        IReadOnlyList<Transaction>? handed = null;
        _engine.Setup(e => e.Build(It.IsAny<IEnumerable<BudgetPlan>>(), It.IsAny<IReadOnlyList<Transaction>>(), It.IsAny<IEnumerable<Category>>(), Today, 12))
            .Callback<IEnumerable<BudgetPlan>, IReadOnlyList<Transaction>, IEnumerable<Category>, DateTime, int>((_, t, _, _, _) => handed = t)
            .Returns(Result<MoneyMap>.Success(new MoneyMap(null, Today, Today, 12, 0m, 0m, [], [])));

        await BuildSut().GetAsync(UserId, 12);

        Assert.Equal([2, 50, 49], handed!.Select(t => t.Id));
    }

    // ── Bounded read gives the same map as the whole ledger ─────────────────

    /// <summary>
    /// A fake ledger whose reads honour their filters like the real accessor, so the real engine can be run on what the
    /// manager fetches and compared with a run on every row.
    /// </summary>
    private void StubLedger(IReadOnlyList<Transaction> ledger, BudgetPlan[] plans, Category[] categories)
    {
        _planAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(plans);
        _categoryAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(categories);
        _txnAccessor.Setup(a => a.GetFilteredAsync(UserId, It.IsAny<TransactionFilter>()))
            .ReturnsAsync((int _, TransactionFilter f) => ledger
                .Where(t => f.From is not DateTime from || t.OccurredAt >= from.Date)
                .Where(t => f.To is not DateTime to || t.OccurredAt.Date <= to.Date)
                .ToList());
        _txnAccessor.Setup(a => a.GetNewestIncomeAsync(
                UserId, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<decimal?>(), It.IsAny<int>()))
            .ReturnsAsync((int _, IReadOnlyCollection<int> ids, DateTime onOrBefore, decimal? min, int take) => ledger
                .Where(t => t.TransactionType == "Income" && t.CategoryId is int id && ids.Contains(id))
                .Where(t => t.OccurredAt.Date <= onOrBefore.Date && (min is null || t.Amount >= min))
                .OrderByDescending(t => t.OccurredAt).ThenByDescending(t => t.Id)
                .Take(take)
                .ToList());
    }

    private static BudgetPlan PayPlan() => new()
    {
        Name = "Plan",
        IsActive = true,
        PlanMonth = new DateTime(2020, 1, 1),
        NetIncomeMonthly = 4000m,
        Entries =
        [
            new() { CategoryId = 1, LineType = "Income", Bucket = "Core", Cadence = "Biweekly", Amount = 2000m, MonthlyEquivalent = 4333m },
            new() { CategoryId = 2, LineType = "Expense", Bucket = "Core", Cadence = "Monthly", Amount = 400m, MonthlyEquivalent = 400m },
            new() { CategoryId = 3, LineType = "Expense", Bucket = "Core", Cadence = "Annual", Amount = 1200m, MonthlyEquivalent = 100m }
        ]
    };

    private static readonly Category[] PayCategories =
    [
        new() { Id = 1, Name = "Pay" }, new() { Id = 2, Name = "Groceries" }, new() { Id = 3, Name = "Insurance" }
    ];

    private static Transaction Row(int id, int? categoryId, decimal amount, DateTime on, string type) => new()
    {
        Id = id, CategoryId = categoryId, Amount = amount, OccurredAt = on, TransactionType = type, Payee = $"P{categoryId}"
    };

    private async Task AssertBoundedEqualsWholeLedgerAsync(IReadOnlyList<Transaction> ledger, int months)
    {
        var engine = new MoneyMapEngine();
        var plans = new[] { PayPlan() };
        StubLedger(ledger, plans, PayCategories);

        var bounded = await BuildSut(engine).GetAsync(UserId, months);
        var whole = engine.Build(plans, ledger, PayCategories, Today, months);

        Assert.True(bounded.IsSuccess, bounded.Error);
        Assert.Equal(JsonSerializer.Serialize(whole.Value), JsonSerializer.Serialize(bounded.Value));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(24)]
    public async Task Should_MatchTheWholeLedgerMap_ForYearsOfBiweeklyPayAndSpend(int months)
    {
        var ledger = new List<Transaction>();
        var id = 1;
        // Three years of biweekly pay ending a week ago, monthly groceries, a yearly insurance bill, and a future row.
        for (var day = Today.AddDays(-7); day > Today.AddYears(-3); day = day.AddDays(-14))
            ledger.Add(Row(id++, 1, 2000m + (id % 3), day, "Income"));
        for (var m = 0; m < 36; m++)
            ledger.Add(Row(id++, 2, -350m - m, Today.AddMonths(-m).AddDays(-3), "Expense"));
        for (var y = 0; y < 3; y++)
            ledger.Add(Row(id++, 3, -1200m, Today.AddYears(-y).AddMonths(-2), "Expense"));
        ledger.Add(Row(id++, 2, -999m, Today.AddDays(3), "Expense"));

        await AssertBoundedEqualsWholeLedgerAsync(ledger, months);
    }

    [Fact]
    public async Task Should_MatchTheWholeLedgerMap_When_TheLastPaychecksAreOlderThanTheBoundedRange()
    {
        // Pay stopped two years ago: the extra-paycheck projection still reads those checks, so they must be fetched.
        var stop = Today.AddYears(-2);
        var ledger = Enumerable.Range(0, 10)
            .Select(i => Row(i + 1, 1, i == 3 ? 400m : 2000m, stop.AddDays(-14 * i), "Income"))
            .Append(Row(100, 2, -50m, Today.AddDays(-1), "Expense"))
            .ToList();

        await AssertBoundedEqualsWholeLedgerAsync(ledger, 12);
    }

    [Fact]
    public async Task Should_MatchTheWholeLedgerMap_When_SmallIncomeRowsInterleaveRecentPaychecks()
    {
        // Several sub-floor rows between paychecks, and a negative newest row, change which rows count as paychecks.
        var ledger = new List<Transaction>();
        var id = 1;
        for (var i = 0; i < 12; i++)
        {
            ledger.Add(Row(id++, 1, 1800m + i, Today.AddDays(-2 - 14 * i), "Income"));
            ledger.Add(Row(id++, 1, 25m, Today.AddDays(-5 - 14 * i), "Income"));
        }
        ledger.Add(Row(id, 1, -100m, Today, "Income"));

        await AssertBoundedEqualsWholeLedgerAsync(ledger, 3);
    }
}
