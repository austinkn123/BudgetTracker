using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class FeedbackManagerTests
{
    private const int UserId = 5;
    private static readonly DateTime Now = new(2026, 11, 28, 1, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IFeedbackAccessor> _accessor = new(MockBehavior.Strict);
    private readonly List<string> _calls = [];

    private FeedbackManager BuildSut() => new(new FeedbackEngine(), _accessor.Object, new FixedClock(Now));

    private void GivenSent(int lastHour, int lastDay)
    {
        _accessor.Setup(a => a.CountSinceAsync(UserId, Now.AddHours(-1))).Callback(() => _calls.Add("count-hour")).ReturnsAsync(lastHour);
        _accessor.Setup(a => a.CountSinceAsync(UserId, Now.AddDays(-1))).Callback(() => _calls.Add("count-day")).ReturnsAsync(lastDay);
    }

    [Fact]
    public async Task Should_SaveTheNormalizedEntry_When_ValidAndUnderTheLimit()
    {
        GivenSent(0, 0);
        Feedback? saved = null;
        _accessor.Setup(a => a.CreateAsync(It.IsAny<Feedback>()))
            .Callback<Feedback>(f => { saved = f; _calls.Add("create"); })
            .ReturnsAsync(9);

        var result = await BuildSut().SubmitAsync(new Feedback { UserId = UserId, Message = "  nice  ", Page = "/budget?x=1" });

        Assert.Equal(9, result.Value);
        Assert.Equal(("nice", "/budget", Now), (saved!.Message, saved.Page, saved.CreatedAt));
        Assert.Equal(["count-hour", "count-day", "create"], _calls);
    }

    [Fact]
    public async Task Should_FailWithoutTouchingTheDatabase_When_MessageIsEmpty()
    {
        var result = await BuildSut().SubmitAsync(new Feedback { UserId = UserId, Message = "   " });

        Assert.Equal("Feedback can't be empty", result.Error);
        _accessor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Should_FailWithRateLimited_When_OverTheHourlyLimit()
    {
        GivenSent(FeedbackLimits.MaxPerHour, FeedbackLimits.MaxPerHour);

        var result = await BuildSut().SubmitAsync(new Feedback { UserId = UserId, Message = "again" });

        Assert.Equal(FeedbackErrors.RateLimited, result.Error);
        _accessor.Verify(a => a.CreateAsync(It.IsAny<Feedback>()), Times.Never());
    }

    [Fact]
    public async Task Should_FailWithRateLimited_When_OverTheDailyLimit()
    {
        GivenSent(0, FeedbackLimits.MaxPerDay);

        var result = await BuildSut().SubmitAsync(new Feedback { UserId = UserId, Message = "again" });

        Assert.Equal(FeedbackErrors.RateLimited, result.Error);
    }

    [Fact]
    public async Task Should_ReturnAPageWithTheClampedSize()
    {
        var created = new DateTime(2026, 11, 27);
        _accessor.Setup(a => a.GetPageAsync(40, FeedbackLimits.MaxPageSize))
            .ReturnsAsync([new Feedback { Id = 39, UserId = 2, Message = "hi", Page = "/", CreatedAt = created }]);

        var result = await BuildSut().GetPageAsync(40, 5000);

        var view = Assert.Single(result.Value!);
        Assert.Equal(new FeedbackView(39, 2, "hi", "/", created), view);
        Assert.Equal(DateTimeKind.Utc, view.CreatedAt.Kind);
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
