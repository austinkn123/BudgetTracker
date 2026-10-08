using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Endpoints;
using Microsoft.AspNetCore.Http;
using Moq;

namespace BudgetTracker.Tests.Endpoints;

/// <summary>
/// Handler-level tests for category create/update: validation failures (including duplicate names) are a 400 with an
/// <c>{ error }</c> body, never a 500 from the unique index.
/// </summary>
public class CategoryEndpointTests
{
    private const string Duplicate = "A category named \"Food\" already exists";

    private readonly Mock<ICategoryManager> _manager = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUserProvider> _currentUser = new();

    public CategoryEndpointTests()
    {
        _currentUser.SetupGet(u => u.UserId).Returns(5);
    }

    [Fact]
    public async Task Create_Success_Returns201()
    {
        _manager.Setup(m => m.CreateAsync(It.Is<Category>(c => c.UserId == 5))).ReturnsAsync(Result<int>.Success(12));

        var result = await CategoryEndpoints.CreateAsync(new Category { Name = "Food" }, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status201Created, EndpointAssert.StatusOf(result));
    }

    [Fact]
    public async Task Create_DuplicateName_Returns400WithErrorBody()
    {
        _manager.Setup(m => m.CreateAsync(It.IsAny<Category>())).ReturnsAsync(Result<int>.Failure(Duplicate));

        var result = await CategoryEndpoints.CreateAsync(new Category { Name = "Food" }, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Equal(Duplicate, EndpointAssert.ErrorOf(result));
    }

    [Fact]
    public async Task Update_DuplicateName_Returns400WithErrorBody()
    {
        _manager.Setup(m => m.UpdateAsync(It.IsAny<Category>())).ReturnsAsync(Result<bool>.Failure(Duplicate));

        var result = await CategoryEndpoints.UpdateAsync(8, new Category { Id = 8, Name = "Food" }, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Equal(Duplicate, EndpointAssert.ErrorOf(result));
    }

    [Fact]
    public async Task Update_MissingCategory_Returns404()
    {
        _manager.Setup(m => m.UpdateAsync(It.IsAny<Category>())).ReturnsAsync(Result<bool>.Failure("Category not found"));

        var result = await CategoryEndpoints.UpdateAsync(8, new Category { Id = 8, Name = "Food" }, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status404NotFound, EndpointAssert.StatusOf(result));
    }

    [Fact]
    public async Task Update_IdMismatch_Returns400WithErrorBody()
    {
        var result = await CategoryEndpoints.UpdateAsync(8, new Category { Id = 9, Name = "Food" }, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Equal("ID mismatch", EndpointAssert.ErrorOf(result));
    }
}
