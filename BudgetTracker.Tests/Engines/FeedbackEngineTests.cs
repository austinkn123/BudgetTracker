using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Tests.Engines;

public class FeedbackEngineTests
{
    private readonly FeedbackEngine _sut = new();

    private static Feedback Entry(string message) => new() { UserId = 5, Message = message };

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  love the money map  ", "love the money map")]
    public void Should_TrimTheMessage(string? input, string expected) =>
        Assert.Equal(expected, _sut.NormalizeMessage(input));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("budget", null)]
    [InlineData("https://evil.example/x", null)]
    [InlineData(" /budget ", "/budget")]
    [InlineData("/plaid-oauth?oauth_state_id=secret", "/plaid-oauth")]
    [InlineData("/transactions#row-5", "/transactions")]
    public void Should_KeepOnlyAnAppPath_When_NormalizingThePage(string? input, string? expected) =>
        Assert.Equal(expected, _sut.NormalizePage(input));

    [Fact]
    public void Should_CapThePageAtTheMaxLength()
    {
        var page = _sut.NormalizePage("/" + new string('a', 500));

        Assert.Equal(FeedbackLimits.MaxPageLength, page!.Length);
    }

    [Fact]
    public void Should_AcceptAMessage_When_ItHasText() => Assert.Null(_sut.Validate(Entry("Dark mode is great")));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_RejectAnEmptyMessage(string message) =>
        Assert.Equal("Feedback can't be empty", _sut.Validate(Entry(message)));

    [Fact]
    public void Should_AcceptAMessage_When_ItIsExactlyTheMaxLength() =>
        Assert.Null(_sut.Validate(Entry(new string('a', FeedbackLimits.MaxMessageLength))));

    [Fact]
    public void Should_RejectAMessage_When_ItIsOverTheMaxLength() =>
        Assert.Equal("Feedback must be 2000 characters or fewer", _sut.Validate(Entry(new string('a', FeedbackLimits.MaxMessageLength + 1))));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(FeedbackLimits.MaxPerHour - 1, FeedbackLimits.MaxPerDay - 1)]
    public void Should_Allow_When_UnderBothLimits(int hour, int day) => Assert.Null(_sut.CheckRateLimit(hour, day));

    [Theory]
    [InlineData(FeedbackLimits.MaxPerHour, FeedbackLimits.MaxPerHour)]
    [InlineData(0, FeedbackLimits.MaxPerDay)]
    public void Should_RateLimit_When_EitherLimitIsReached(int hour, int day) =>
        Assert.Equal(FeedbackErrors.RateLimited, _sut.CheckRateLimit(hour, day));

    [Theory]
    [InlineData(null, FeedbackLimits.MaxPageSize)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(25, 25)]
    [InlineData(1000, FeedbackLimits.MaxPageSize)]
    public void Should_ClampThePageSize(int? take, int expected) => Assert.Equal(expected, _sut.ClampPageSize(take));
}
