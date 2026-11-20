using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace BudgetTracker.Tests.Endpoints;

public class CategoryRuleEndpointTests
{
    private readonly Mock<ICategoryRuleManager> _manager = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUserProvider> _currentUser = new();

    public CategoryRuleEndpointTests() => _currentUser.SetupGet(u => u.UserId).Returns(5);

    private static readonly CategoryRuleRequest Request = new("uber", CategoryRuleMatchType.Contains, 10, ApplyToExisting: true);

    private static bool IsRequestFor(CategoryRule r, int id = 0) =>
        r is { UserId: 5, Pattern: "uber", MatchType: CategoryRuleMatchType.Contains, CategoryId: 10 } && r.Id == id;

    [Fact]
    public async Task Should_ReturnTheUsersRules()
    {
        IReadOnlyList<CategoryRuleView> rules = [new(1, "uber", "Contains", 10, "Coffee", 3)];
        _manager.Setup(m => m.GetAllAsync(5)).ReturnsAsync(Result<IReadOnlyList<CategoryRuleView>>.Success(rules));

        var result = await CategoryRuleEndpoints.GetAllAsync(_manager.Object, _currentUser.Object);

        Assert.Same(rules, Assert.IsType<Ok<IReadOnlyList<CategoryRuleView>>>(result).Value);
    }

    [Fact]
    public async Task Should_CreateForTheCurrentUserAndPassApplyToExisting()
    {
        _manager.Setup(m => m.CreateAsync(It.Is<CategoryRule>(r => IsRequestFor(r, 0)), true))
            .ReturnsAsync(Result<CategoryRuleCreated>.Success(new(7, 4)));

        var result = await CategoryRuleEndpoints.CreateAsync(Request, _manager.Object, _currentUser.Object);

        var created = Assert.IsType<Created<CategoryRuleCreated>>(result);
        Assert.Equal(("/api/category-rules/7", new CategoryRuleCreated(7, 4)), (created.Location, created.Value));
    }

    [Fact]
    public async Task Should_Return400WithError_When_CreateFails()
    {
        _manager.Setup(m => m.CreateAsync(It.IsAny<CategoryRule>(), true))
            .ReturnsAsync(Result<CategoryRuleCreated>.Failure("A rule for \"uber\" already exists"));

        var result = await CategoryRuleEndpoints.CreateAsync(Request, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Equal("A rule for \"uber\" already exists", EndpointAssert.ErrorOf(result));
    }

    [Fact]
    public async Task Should_ReturnTheCount_When_Previewing()
    {
        _manager.Setup(m => m.PreviewAsync(It.Is<CategoryRule>(r => IsRequestFor(r, 0)))).ReturnsAsync(Result<int>.Success(4));

        var result = await CategoryRuleEndpoints.PreviewAsync(Request, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
        var body = Assert.IsAssignableFrom<IValueHttpResult>(result).Value!;
        Assert.Equal(4, body.GetType().GetProperty("count")!.GetValue(body));
    }

    [Fact]
    public async Task Should_Return400WithError_When_PreviewIsInvalid()
    {
        _manager.Setup(m => m.PreviewAsync(It.IsAny<CategoryRule>())).ReturnsAsync(Result<int>.Failure("Pattern is required"));

        var result = await CategoryRuleEndpoints.PreviewAsync(Request, _manager.Object, _currentUser.Object);

        Assert.Equal("Pattern is required", EndpointAssert.ErrorOf(result));
    }

    [Theory]
    [InlineData(true, null, StatusCodes.Status204NoContent)]
    [InlineData(false, "Rule not found", StatusCodes.Status404NotFound)]
    [InlineData(false, "Category not found", StatusCodes.Status400BadRequest)]
    public async Task Should_MapUpdateResults(bool success, string? error, int status)
    {
        _manager.Setup(m => m.UpdateAsync(It.Is<CategoryRule>(r => IsRequestFor(r, 3))))
            .ReturnsAsync(success ? Result<bool>.Success(true) : Result<bool>.Failure(error!));

        var result = await CategoryRuleEndpoints.UpdateAsync(3, Request, _manager.Object, _currentUser.Object);

        Assert.Equal(status, EndpointAssert.StatusOf(result));
    }

    [Theory]
    [InlineData(true, StatusCodes.Status204NoContent)]
    [InlineData(false, StatusCodes.Status404NotFound)]
    public async Task Should_MapDeleteResults(bool success, int status)
    {
        _manager.Setup(m => m.DeleteAsync(3, 5))
            .ReturnsAsync(success ? Result<bool>.Success(true) : Result<bool>.Failure("Rule not found"));

        var result = await CategoryRuleEndpoints.DeleteAsync(3, _manager.Object, _currentUser.Object);

        Assert.Equal(status, EndpointAssert.StatusOf(result));
    }
}
