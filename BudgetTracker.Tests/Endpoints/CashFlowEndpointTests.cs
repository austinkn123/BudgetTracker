using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models.CashFlow;
using BudgetTracker.Server.Endpoints;
using Microsoft.AspNetCore.Http;
using Moq;

namespace BudgetTracker.Tests.Endpoints;

public class CashFlowEndpointTests
{
    private static readonly DateTime From = new(2026, 10, 1);
    private static readonly DateTime To = new(2026, 10, 31);
    private static readonly CashFlowReport Report = new([], new(0m, 0m), null, new(0m, 0m), [], [], null);

    private readonly Mock<ICashFlowManager> _manager = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUserProvider> _currentUser = new();

    public CashFlowEndpointTests()
    {
        _currentUser.SetupGet(u => u.UserId).Returns(5);
    }

    private Task<IResult> Get(string? groupBy) =>
        CashFlowEndpoints.GetAsync(From, To, groupBy, null, null, "chk", 3, "rent", _manager.Object, _currentUser.Object);

    [Theory]
    [InlineData("week")]
    [InlineData("WEEK")]
    [InlineData("Week")]
    public async Task Get_GroupByIsCaseInsensitive_AndFiltersReachTheManager(string groupBy)
    {
        var expected = new CashFlowQuery(From, To, CashFlowGrouping.Week, null, null, "chk", 3, "rent");
        _manager.Setup(m => m.GetReportAsync(5, expected)).ReturnsAsync(Result<CashFlowReport>.Success(Report));

        var result = await Get(groupBy);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("fortnight")]
    [InlineData("7")]          // Enum.TryParse would accept any number
    [InlineData("Day,Week")]   // ...and a flags-style list
    public async Task Get_UnknownGroupBy_Returns400(string? groupBy)
    {
        var result = await Get(groupBy);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.NotNull(EndpointAssert.ErrorOf(result));
    }

    [Fact]
    public async Task Get_ManagerFailure_Returns400WithError()
    {
        _manager.Setup(m => m.GetReportAsync(5, It.IsAny<CashFlowQuery>())).ReturnsAsync(Result<CashFlowReport>.Failure("bad window"));

        var result = await Get("Month");

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Equal("bad window", EndpointAssert.ErrorOf(result));
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public async Task Should_PassTheSameWallClockAsUnspecifiedKind_When_GivenZonedDates(DateTimeKind kind)
    {
        // "…T00:00:00Z" binds as Kind=Utc, which Npgsql refuses against the timestamp-without-time-zone column.
        CashFlowQuery? passed = null;
        _manager.Setup(m => m.GetReportAsync(5, It.IsAny<CashFlowQuery>()))
            .Callback<int, CashFlowQuery>((_, q) => passed = q)
            .ReturnsAsync(Result<CashFlowReport>.Success(Report));

        var result = await CashFlowEndpoints.GetAsync(
            new DateTime(2026, 10, 1, 0, 0, 0, kind), new DateTime(2026, 10, 31, 0, 0, 0, kind), "Month",
            new DateTime(2026, 9, 1, 0, 0, 0, kind), new DateTime(2026, 9, 30, 12, 0, 0, kind),
            null, null, null, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
        AssertUnzoned(new DateTime(2026, 10, 1), passed!.From);
        AssertUnzoned(new DateTime(2026, 10, 31), passed.To);
        AssertUnzoned(new DateTime(2026, 9, 1), passed.CompareFrom);
        AssertUnzoned(new DateTime(2026, 9, 30, 12, 0, 0), passed.CompareTo);
    }

    [Fact]
    public async Task Should_LeaveAbsentOptionalDatesNull_When_NormalisingKinds()
    {
        CashFlowQuery? passed = null;
        _manager.Setup(m => m.GetReportAsync(5, It.IsAny<CashFlowQuery>()))
            .Callback<int, CashFlowQuery>((_, q) => passed = q)
            .ReturnsAsync(Result<CashFlowReport>.Success(Report));

        await CashFlowEndpoints.GetAsync(null, To, "Month", null, null, null, null, null, _manager.Object, _currentUser.Object);

        Assert.Equal((null, null, null), (passed!.From, passed.CompareFrom, passed.CompareTo));
    }

    // DateTime equality ignores Kind, so the kind is asserted separately.
    private static void AssertUnzoned(DateTime expected, DateTime? actual) =>
        Assert.Equal((expected, DateTimeKind.Unspecified), (actual!.Value, actual.Value.Kind));
}
