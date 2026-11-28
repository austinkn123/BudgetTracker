using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace BudgetTracker.Tests.Endpoints;

public class FeedbackEndpointTests
{
    private readonly Mock<IFeedbackManager> _manager = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUserProvider> _currentUser = new();

    public FeedbackEndpointTests() => _currentUser.SetupGet(u => u.UserId).Returns(5);

    [Fact]
    public async Task Should_SubmitForTheCurrentUserAndReturn201()
    {
        _manager.Setup(m => m.SubmitAsync(It.Is<Feedback>(f => f.UserId == 5 && f.Message == "hi" && f.Page == "/budget")))
            .ReturnsAsync(Result<int>.Success(7));

        var result = await FeedbackEndpoints.SubmitAsync(new FeedbackRequest("hi", "/budget"), _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status201Created, EndpointAssert.StatusOf(result));
        Assert.Equal("/api/feedback/7", result.GetType().GetProperty("Location")!.GetValue(result));
        var body = Assert.IsAssignableFrom<IValueHttpResult>(result).Value!;
        Assert.Equal(7, body.GetType().GetProperty("id")!.GetValue(body));
    }

    [Fact]
    public async Task Should_PassAnEmptyMessage_When_BodyHasNone()
    {
        _manager.Setup(m => m.SubmitAsync(It.Is<Feedback>(f => f.Message == string.Empty)))
            .ReturnsAsync(Result<int>.Failure("Feedback can't be empty"));

        var result = await FeedbackEndpoints.SubmitAsync(new FeedbackRequest(null, null), _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Equal("Feedback can't be empty", EndpointAssert.ErrorOf(result));
    }

    [Fact]
    public async Task Should_Return429_When_RateLimited()
    {
        _manager.Setup(m => m.SubmitAsync(It.IsAny<Feedback>())).ReturnsAsync(Result<int>.Failure(FeedbackErrors.RateLimited));

        var result = await FeedbackEndpoints.SubmitAsync(new FeedbackRequest("hi", null), _manager.Object, _currentUser.Object);

        Assert.Equal(StatusCodes.Status429TooManyRequests, EndpointAssert.StatusOf(result));
    }

    [Fact]
    public async Task Should_ReturnThePage()
    {
        IReadOnlyList<FeedbackView> page = [new(3, 1, "hi", null, new DateTime(2026, 11, 27))];
        _manager.Setup(m => m.GetPageAsync(10, 20)).ReturnsAsync(Result<IReadOnlyList<FeedbackView>>.Success(page));

        var result = await FeedbackEndpoints.GetPageAsync(_manager.Object, before: 10, take: 20);

        Assert.Same(page, Assert.IsType<Ok<IReadOnlyList<FeedbackView>>>(result).Value);
    }
}
