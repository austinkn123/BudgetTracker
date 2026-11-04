namespace BudgetTracker.Domain.Webull;

/// <summary>
/// Utility: the US-Eastern calendar. Pure, no I/O. The single home of the Eastern time zone (hard-coded 2007+ DST rule,
/// no OS tzdata), so every engine that needs an Eastern date calls this instead of another engine.
/// <see cref="Engines.WebullEngine"/>'s <c>ToEasternDate</c> and 4:30 pm close delegate here.
/// </summary>
public static class EasternTime
{
    // ponytail: 2007+ US rule in code, no OS tzdata dependency (the Lambda image may lack it). Change here if US DST law changes.
    internal static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone(
        "US-Eastern", TimeSpan.FromHours(-5), "US Eastern", "EST", "EDT",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(2007, 1, 1), DateTime.MaxValue.Date, TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday))
        ]);

    /// <summary>The US-Eastern calendar date of <paramref name="utc"/> (any kind is read as UTC).</summary>
    public static DateOnly ToEasternDate(DateTime utc) => DateOnly.FromDateTime(ToEastern(utc));

    /// <summary>
    /// First Eastern date of <paramref name="range"/>, inclusive, ending <paramref name="today"/>: OneMonth = today − 1 month,
    /// ThreeMonths = today − 3 months, YearToDate = January 1 of today's year, OneYear = today − 1 year, All = null.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="range"/> is not a defined value.</exception>
    public static DateOnly? RangeStart(WebullRange range, DateOnly today) => range switch
    {
        WebullRange.OneMonth => today.AddMonths(-1),
        WebullRange.ThreeMonths => today.AddMonths(-3),
        WebullRange.YearToDate => new DateOnly(today.Year, 1, 1),
        WebullRange.OneYear => today.AddYears(-1),
        WebullRange.All => null,
        _ => throw new ArgumentOutOfRangeException(nameof(range), range, "Unknown range.")
    };

    /// <summary>The Eastern wall-clock time of <paramref name="utc"/> (any kind is read as UTC).</summary>
    internal static DateTime ToEastern(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
}
