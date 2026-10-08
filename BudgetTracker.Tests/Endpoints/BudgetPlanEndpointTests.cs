using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Endpoints;
using Microsoft.AspNetCore.Http;
using Moq;

namespace BudgetTracker.Tests.Endpoints;

/// <summary>
/// Handler-level tests for budget plan create/update: validation failures (including duplicate names) are a 400 with
/// an <c>{ error }</c> body, never a 500 from the unique index or a misleading 404.
/// </summary>
public class BudgetPlanEndpointTests
{
    private const string Duplicate = "A budget plan named \"Baseline\" already exists for this month";

    private readonly Mock<IBudgetPlanManager> _manager = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUserProvider> _currentUser = new();

    public BudgetPlanEndpointTests()
    {
        _currentUser.SetupGet(u => u.UserId).Returns(5);
    }

    [Fact]
    public async Task Create_Success_Returns201()
    {
        _manager.Setup(m => m.CreateAsync(It.IsAny<BudgetPlan>(), 5)).ReturnsAsync(Result<int>.Success(3));

        var result = await BudgetPlanEndpoints.CreateAsync(new BudgetPlan { Name = "Baseline" }, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status201Created, EndpointAssert.StatusOf(result));
    }

    [Fact]
    public async Task Create_DuplicateName_Returns400WithErrorBody()
    {
        _manager.Setup(m => m.CreateAsync(It.IsAny<BudgetPlan>(), 5)).ReturnsAsync(Result<int>.Failure(Duplicate));

        var result = await BudgetPlanEndpoints.CreateAsync(new BudgetPlan { Name = "Baseline" }, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Equal(Duplicate, EndpointAssert.ErrorOf(result));
    }

    [Fact]
    public async Task Update_DuplicateName_Returns400WithErrorBody()
    {
        _manager.Setup(m => m.UpdateAsync(It.IsAny<BudgetPlan>(), 5)).ReturnsAsync(Result<bool>.Failure(Duplicate));

        var result = await BudgetPlanEndpoints.UpdateAsync(7, new BudgetPlan { Id = 7, Name = "Baseline" }, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Equal(Duplicate, EndpointAssert.ErrorOf(result));
    }

    [Fact]
    public async Task Update_MissingPlan_Returns404()
    {
        _manager.Setup(m => m.UpdateAsync(It.IsAny<BudgetPlan>(), 5)).ReturnsAsync(Result<bool>.Failure("Budget plan not found"));

        var result = await BudgetPlanEndpoints.UpdateAsync(7, new BudgetPlan { Id = 7, Name = "Baseline" }, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status404NotFound, EndpointAssert.StatusOf(result));
    }

    [Fact]
    public async Task Update_IdMismatch_Returns400WithErrorBody()
    {
        var result = await BudgetPlanEndpoints.UpdateAsync(7, new BudgetPlan { Id = 8, Name = "Baseline" }, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Equal("ID mismatch", EndpointAssert.ErrorOf(result));
    }
}
