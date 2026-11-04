using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.CashFlow;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class CashFlowManagerTests
{
    private const int UserId = 5;

    private readonly Mock<ICashFlowEngine> _engine = new(MockBehavior.Strict);
    private readonly Mock<ITransactionAccessor> _txnAccessor = new(MockBehavior.Strict);
    private readonly Mock<IPlaidItemAccessor> _itemAccessor = new(MockBehavior.Strict);

    private static readonly CashFlowReport Report = new([], new(0m, 0m), null, new(0m, 0m), [], [], null);

    private CashFlowManager BuildSut() => new(_engine.Object, _txnAccessor.Object, _itemAccessor.Object);

    private void StubEngine(CashFlowQuery query) =>
        _engine.Setup(e => e.BuildReport(query, It.IsAny<IReadOnlyList<Transaction>>(), It.IsAny<IReadOnlyList<PlaidAccount>>(), It.IsAny<DateTime>()))
            .Returns(Result<CashFlowReport>.Success(Report));

    [Fact]
    public async Task GetReport_FetchesFromTheEarlierWindowStartWithTheFiltersAndNoUpperBound()
    {
        // Rows after To are needed to reconstruct period balances, and the compare window may start first.
        var query = new CashFlowQuery(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), CashFlowGrouping.Week,
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), "chk", 3, "rent");
        var expected = new TransactionFilter(From: new DateTime(2026, 9, 1), Search: "rent", PlaidAccountId: "chk", CategoryId: 3);
        _txnAccessor.Setup(a => a.GetFilteredAsync(UserId, expected)).ReturnsAsync([]);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([]);
        StubEngine(query);

        var result = await BuildSut().GetReportAsync(UserId, query);

        Assert.True(result.IsSuccess);
        Assert.Same(Report, result.Value);
    }

    [Fact]
    public async Task GetReport_AllRange_FetchesEverything()
    {
        var query = new CashFlowQuery(null, new DateTime(2026, 10, 31), CashFlowGrouping.Month, new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));
        var everything = new TransactionFilter();
        _txnAccessor.Setup(a => a.GetFilteredAsync(UserId, everything)).ReturnsAsync([]);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([]);
        StubEngine(query);

        var result = await BuildSut().GetReportAsync(UserId, query);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetReport_HandsTheEngineEveryLinkedAccountAndTheRows()
    {
        var query = new CashFlowQuery(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), CashFlowGrouping.Month);
        Transaction[] rows = [new() { Id = 1 }];
        var fromOnward = new TransactionFilter(From: query.From);
        _txnAccessor.Setup(a => a.GetFilteredAsync(UserId, fromOnward)).ReturnsAsync(rows);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync(
        [
            new PlaidItem { Id = 1, Accounts = [new PlaidAccount { PlaidAccountId = "chk" }] },
            new PlaidItem { Id = 2, Accounts = [new PlaidAccount { PlaidAccountId = "card" }, new PlaidAccount { PlaidAccountId = "sav" }] }
        ]);
        _engine.Setup(e => e.BuildReport(
                query,
                It.Is<IReadOnlyList<Transaction>>(t => t.Single().Id == 1),
                It.Is<IReadOnlyList<PlaidAccount>>(a => a.Select(x => x.PlaidAccountId).SequenceEqual(new[] { "chk", "card", "sav" })),
                It.Is<DateTime>(today => today.TimeOfDay == TimeSpan.Zero)))
            .Returns(Result<CashFlowReport>.Success(Report));

        var result = await BuildSut().GetReportAsync(UserId, query);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetReport_EngineFailure_IsReturned()
    {
        var query = new CashFlowQuery(new DateTime(2026, 11, 1), new DateTime(2026, 10, 1), CashFlowGrouping.Month);
        _txnAccessor.Setup(a => a.GetFilteredAsync(UserId, It.IsAny<TransactionFilter>())).ReturnsAsync([]);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([]);
        _engine.Setup(e => e.BuildReport(query, It.IsAny<IReadOnlyList<Transaction>>(), It.IsAny<IReadOnlyList<PlaidAccount>>(), It.IsAny<DateTime>()))
            .Returns(Result<CashFlowReport>.Failure("bad window"));

        var result = await BuildSut().GetReportAsync(UserId, query);

        Assert.False(result.IsSuccess);
        Assert.Equal("bad window", result.Error);
    }
}
