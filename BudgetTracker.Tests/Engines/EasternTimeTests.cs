using System.Globalization;
using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Tests.Engines;

/// <summary>
/// Tests for the <see cref="EasternTime"/> utility: the US-Eastern date of a UTC instant on both sides of each DST change,
/// and the first Eastern date of every range, including month-end clamping, leap days and year boundaries.
/// </summary>
public class EasternTimeTests
{
    private static DateTime Utc(string iso) =>
        DateTime.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

    [Theory]
    [InlineData("2026-03-08T04:59:00Z", "2026-03-07")] // EST: 23:59 Sat
    [InlineData("2026-03-08T05:00:00Z", "2026-03-08")] // EST: 00:00 Sun, the day DST starts
    [InlineData("2026-03-09T03:59:00Z", "2026-03-08")] // EDT: 23:59 Sun
    [InlineData("2026-03-09T04:00:00Z", "2026-03-09")] // EDT: 00:00 Mon
    [InlineData("2026-11-01T03:59:00Z", "2026-10-31")] // EDT: 23:59 Sat
    [InlineData("2026-11-01T04:00:00Z", "2026-11-01")] // EDT: 00:00 Sun, the day DST ends
    [InlineData("2026-11-02T04:59:00Z", "2026-11-01")] // EST: 23:59 Sun
    [InlineData("2026-11-02T05:00:00Z", "2026-11-02")] // EST: 00:00 Mon
    [InlineData("2027-01-01T04:59:59Z", "2026-12-31")] // EST: New Year's Eve, the UTC year has already turned
    [InlineData("2027-01-01T05:00:00Z", "2027-01-01")]
    public void ToEasternDate_FollowsUsDaylightSavingRule(string utc, string expected) =>
        Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), EasternTime.ToEasternDate(Utc(utc)));

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void ToEasternDate_AnyKind_IsReadAsUtc(DateTimeKind kind) =>
        Assert.Equal(new DateOnly(2026, 3, 7), EasternTime.ToEasternDate(new DateTime(2026, 3, 8, 4, 59, 0, kind)));

    [Theory]
    [InlineData(WebullRange.OneMonth, "2026-10-25", "2026-09-25")]
    [InlineData(WebullRange.OneMonth, "2026-03-31", "2026-02-28")] // clamps to the shorter month
    [InlineData(WebullRange.OneMonth, "2024-03-31", "2024-02-29")] // leap year
    [InlineData(WebullRange.OneMonth, "2026-01-15", "2025-12-15")] // crosses the year
    [InlineData(WebullRange.ThreeMonths, "2026-10-25", "2026-07-25")]
    [InlineData(WebullRange.ThreeMonths, "2026-05-31", "2026-02-28")]
    [InlineData(WebullRange.ThreeMonths, "2026-02-10", "2025-11-10")]
    [InlineData(WebullRange.YearToDate, "2026-10-25", "2026-01-01")]
    [InlineData(WebullRange.YearToDate, "2026-01-01", "2026-01-01")] // New Year's Day: a one-day range
    [InlineData(WebullRange.YearToDate, "2026-12-31", "2026-01-01")]
    [InlineData(WebullRange.OneYear, "2026-10-25", "2025-10-25")]
    [InlineData(WebullRange.OneYear, "2024-02-29", "2023-02-28")]
    [InlineData(WebullRange.OneYear, "2026-01-01", "2025-01-01")]
    public void RangeStart_IsTheFirstEasternDateOfTheRange(WebullRange range, string today, string expected) =>
        Assert.Equal(
            DateOnly.Parse(expected, CultureInfo.InvariantCulture),
            EasternTime.RangeStart(range, DateOnly.Parse(today, CultureInfo.InvariantCulture)));

    [Fact]
    public void RangeStart_All_IsNull() =>
        Assert.Null(EasternTime.RangeStart(WebullRange.All, new DateOnly(2026, 10, 25)));

    [Fact]
    public void RangeStart_UnknownRange_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => EasternTime.RangeStart((WebullRange)99, new DateOnly(2026, 10, 25)));

    [Theory]
    // "Today" is the Eastern date, so a UTC instant past midnight UTC still belongs to the previous Eastern day or year.
    [InlineData("2027-01-01T04:30:00Z", WebullRange.YearToDate, "2026-01-01")] // 23:30 EST Dec 31
    [InlineData("2027-01-01T05:00:00Z", WebullRange.YearToDate, "2027-01-01")] // 00:00 EST Jan 1
    [InlineData("2026-11-01T03:59:00Z", WebullRange.OneMonth, "2026-09-30")] // 23:59 EDT Oct 31, just before DST ends
    [InlineData("2026-11-01T04:00:00Z", WebullRange.OneMonth, "2026-10-01")] // 00:00 EDT Nov 1, the day DST ends
    [InlineData("2026-03-08T04:59:00Z", WebullRange.ThreeMonths, "2025-12-07")] // 23:59 EST Mar 7, the eve of DST
    [InlineData("2026-03-08T05:00:00Z", WebullRange.OneYear, "2025-03-08")] // 00:00 EST Mar 8, the day DST starts
    public void RangeStart_OfTodayInEasternTime_HandlesDstAndYearBoundaries(string nowUtc, WebullRange range, string expected) =>
        Assert.Equal(
            DateOnly.Parse(expected, CultureInfo.InvariantCulture),
            EasternTime.RangeStart(range, EasternTime.ToEasternDate(Utc(nowUtc))));
}
