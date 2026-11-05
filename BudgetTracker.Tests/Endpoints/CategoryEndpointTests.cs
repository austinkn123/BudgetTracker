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

    // ── GET /api/categories/usage ───────────────────────────────────────────

    [Fact]
    public async Task Usage_WithoutSince_Returns400WithErrorBody()
    {
        var result = await CategoryEndpoints.GetUsageAsync(null, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Contains("since", EndpointAssert.ErrorOf(result));
    }

    [Fact]
    public async Task Usage_WithSince_Returns200WithTheUsage()
    {
        var since = new DateTime(2026, 9, 1);
        IReadOnlyList<CategoryUsage> usage = [new CategoryUsage(3, 1, 0m, -5m)];
        _manager.Setup(m => m.GetUsageAsync(5, since)).ReturnsAsync(Result<IReadOnlyList<CategoryUsage>>.Success(usage));

        var result = await CategoryEndpoints.GetUsageAsync(since, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
        Assert.Same(usage, Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public async Task Usage_WithAZonedSince_PassesTheSameWallClockAsUnspecifiedKind(DateTimeKind kind)
    {
        // "2026-09-01T00:00:00Z" binds as Kind=Utc, which Npgsql refuses against a timestamp-without-time-zone column.
        var since = new DateTime(2026, 9, 1, 0, 0, 0, kind);
        DateTime? passed = null;
        _manager.Setup(m => m.GetUsageAsync(5, It.IsAny<DateTime>()))
            .Callback<int, DateTime>((_, d) => passed = d)
            .ReturnsAsync(Result<IReadOnlyList<CategoryUsage>>.Success([]));

        var result = await CategoryEndpoints.GetUsageAsync(since, _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
        Assert.Equal((new DateTime(2026, 9, 1), DateTimeKind.Unspecified), (passed!.Value, passed.Value.Kind));
    }
}
