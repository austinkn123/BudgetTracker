using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models.Analysis;
using BudgetTracker.Server.Endpoints;
using Microsoft.AspNetCore.Http;
using Moq;

namespace BudgetTracker.Tests.Endpoints;

public class BudgetAnalysisEndpointTests
{
    private static readonly DateTime From = new(2026, 10, 1);
    private static readonly DateTime To = new(2026, 10, 31);

    private readonly Mock<IBudgetAnalysisManager> _manager = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUserProvider> _currentUser = new();

    public BudgetAnalysisEndpointTests()
    {
        _currentUser.SetupGet(u => u.UserId).Returns(5);
    }

    [Fact]
    public async Task Should_DefaultTrendMonthsToThree()
    {
        _manager.Setup(m => m.GetAnalysisAsync(5, From, To, 3))
            .ReturnsAsync(Result<BudgetAnalysis>.Success(new BudgetAnalysis(null, [], [])));

        var result = await BudgetAnalysisEndpoints.GetAsync(From, To, _manager.Object, _currentUser.Object, null);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
    }

    [Fact]
    public async Task Should_Return400WithError_When_TheManagerRejectsTrendMonths()
    {
        _manager.Setup(m => m.GetAnalysisAsync(5, From, To, 25))
            .ReturnsAsync(Result<BudgetAnalysis>.Failure("trendMonths must be between 1 and 24"));

        var result = await BudgetAnalysisEndpoints.GetAsync(From, To, _manager.Object, _currentUser.Object, 25);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Equal("trendMonths must be between 1 and 24", EndpointAssert.ErrorOf(result));
    }
}
