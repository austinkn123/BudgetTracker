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
}
