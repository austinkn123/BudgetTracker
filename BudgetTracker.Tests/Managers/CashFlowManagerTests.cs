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

    private static readonly IReadOnlyDictionary<string, decimal> NoTail = new Dictionary<string, decimal>();

    private void StubEngine(CashFlowQuery query) =>
        _engine.Setup(e => e.BuildReport(
                query, It.IsAny<IReadOnlyList<Transaction>>(), It.IsAny<IReadOnlyList<PlaidAccount>>(),
                It.IsAny<IReadOnlyDictionary<string, decimal>>(), It.IsAny<DateTime>()))
            .Returns(Result<CashFlowReport>.Success(Report));

    private void StubTail(DateTime after, IReadOnlyDictionary<string, decimal>? tail = null) =>
        _txnAccessor.Setup(a => a.GetPostedSumByPlaidAccountAfterAsync(UserId, after)).ReturnsAsync(tail ?? NoTail);

    [Fact]
    public async Task GetReport_FetchesFromTheEarlierWindowStartThroughTo_WithTheFilters()
    {
        // The compare window may start first; rows after To are summed in the database, not loaded.
        var query = new CashFlowQuery(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), CashFlowGrouping.Week,
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), "chk", 3, "rent");
        var expected = new TransactionFilter(
            From: new DateTime(2026, 9, 1), To: new DateTime(2026, 10, 31), Search: "rent", PlaidAccountId: "chk", CategoryId: 3);
        _txnAccessor.Setup(a => a.GetFilteredAsync(UserId, expected)).ReturnsAsync([]);
        StubTail(query.To);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([]);
        StubEngine(query);

        var result = await BuildSut().GetReportAsync(UserId, query);

        Assert.True(result.IsSuccess);
        Assert.Same(Report, result.Value);
    }

    [Fact]
    public async Task GetReport_AllRange_FetchesEverythingThroughTo()
    {
        var query = new CashFlowQuery(null, new DateTime(2026, 10, 31), CashFlowGrouping.Month, new DateTime(2026, 9, 1), new DateTime(2026, 9, 30));
        var everything = new TransactionFilter(To: query.To);
        _txnAccessor.Setup(a => a.GetFilteredAsync(UserId, everything)).ReturnsAsync([]);
        StubTail(query.To);
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
        var window = new TransactionFilter(From: query.From, To: query.To);
        IReadOnlyDictionary<string, decimal> tail = new Dictionary<string, decimal> { ["chk"] = -40m };
        _txnAccessor.Setup(a => a.GetFilteredAsync(UserId, window)).ReturnsAsync(rows);
        StubTail(query.To, tail);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync(
        [
            new PlaidItem { Id = 1, Accounts = [new PlaidAccount { PlaidAccountId = "chk" }] },
            new PlaidItem { Id = 2, Accounts = [new PlaidAccount { PlaidAccountId = "card" }, new PlaidAccount { PlaidAccountId = "sav" }] }
        ]);
        _engine.Setup(e => e.BuildReport(
                query,
                It.Is<IReadOnlyList<Transaction>>(t => t.Single().Id == 1),
                It.Is<IReadOnlyList<PlaidAccount>>(a => a.Select(x => x.PlaidAccountId).SequenceEqual(new[] { "chk", "card", "sav" })),
                tail,
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
        StubTail(query.To);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([]);
        _engine.Setup(e => e.BuildReport(
                query, It.IsAny<IReadOnlyList<Transaction>>(), It.IsAny<IReadOnlyList<PlaidAccount>>(),
                It.IsAny<IReadOnlyDictionary<string, decimal>>(), It.IsAny<DateTime>()))
            .Returns(Result<CashFlowReport>.Failure("bad window"));

        var result = await BuildSut().GetReportAsync(UserId, query);

        Assert.False(result.IsSuccess);
        Assert.Equal("bad window", result.Error);
    }

    [Fact]
    public async Task GetReport_CompareWindowEndingAfterTo_ExtendsTheFetchToCompareTo()
    {
        var query = new CashFlowQuery(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), CashFlowGrouping.Month,
            new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
        var throughCompareTo = new TransactionFilter(From: query.From, To: query.CompareTo);
        _txnAccessor.Setup(a => a.GetFilteredAsync(UserId, throughCompareTo)).ReturnsAsync([]);
        StubTail(query.To);
        _itemAccessor.Setup(a => a.GetAllActiveByUserIdAsync(UserId)).ReturnsAsync([]);
        StubEngine(query);

        var result = await BuildSut().GetReportAsync(UserId, query);

        Assert.True(result.IsSuccess);
    }
}
