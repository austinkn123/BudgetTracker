using BudgetTracker.Domain.Common;

namespace BudgetTracker.Tests.Common;

public class LocalTodayTests
{
    private static readonly DateTime UtcNow = new(2026, 11, 16, 2, 30, 0, DateTimeKind.Utc); // 8:30 pm Nov 15 in Central

    [Fact]
    public void Should_UseTheClientsDate_When_ItIsTheDayBeforeUtc() =>
        Assert.Equal(new DateTime(2026, 11, 15), LocalToday.Resolve(new DateTime(2026, 11, 15), UtcNow));

    [Fact]
    public void Should_UseTheClientsDate_When_ItIsTheSameDayAsUtc() =>
        Assert.Equal(new DateTime(2026, 11, 16), LocalToday.Resolve(new DateTime(2026, 11, 16), UtcNow));

    [Fact]
    public void Should_UseTheClientsDate_When_ItIsTheDayAfterUtc() =>
        // Ahead-of-UTC zones (Asia, Oceania) are already on the next date.
        Assert.Equal(new DateTime(2026, 11, 17), LocalToday.Resolve(new DateTime(2026, 11, 17), UtcNow));

    [Theory]
    [InlineData(2026, 11, 14)]
    [InlineData(2026, 11, 18)]
    [InlineData(1970, 1, 1)]
    [InlineData(9999, 12, 31)]
    public void Should_FallBackToTheUtcDate_When_TheClientsDateIsImplausible(int year, int month, int day) =>
        // No real clock is more than a day off UTC; anything further is a bad or forged value.
        Assert.Equal(new DateTime(2026, 11, 16), LocalToday.Resolve(new DateTime(year, month, day), UtcNow));

    [Fact]
    public void Should_FallBackToTheUtcDate_When_NoClientDateIsSent() =>
        Assert.Equal(new DateTime(2026, 11, 16), LocalToday.Resolve(null, UtcNow));

    [Fact]
    public void Should_IgnoreAnyTimeOfDayAndReturnAnUnzonedDate()
    {
        var today = LocalToday.Resolve(new DateTime(2026, 11, 15, 23, 59, 0, DateTimeKind.Utc), UtcNow);

        Assert.Equal(new DateTime(2026, 11, 15), today);
        Assert.Equal(TimeSpan.Zero, today.TimeOfDay);
        Assert.Equal(DateTimeKind.Unspecified, today.Kind);
    }
}
