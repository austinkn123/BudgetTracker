using BudgetTracker.Server.Utilities;

namespace BudgetTracker.Tests.Utilities;

public class WallClockTests
{
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Should_KeepTheWallClockAndDropTheKind(DateTimeKind kind)
    {
        var unzoned = WallClock.Unzoned(new DateTime(2026, 10, 31, 23, 59, 30, kind));

        Assert.Equal((new DateTime(2026, 10, 31, 23, 59, 30), DateTimeKind.Unspecified), (unzoned, unzoned.Kind));
    }

    [Fact]
    public void Should_LeaveNullAsNull() => Assert.Null(WallClock.Unzoned((DateTime?)null));

    [Fact]
    public void Should_UnzoneAPresentNullableValue()
    {
        DateTime? value = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var unzoned = WallClock.Unzoned(value);

        Assert.Equal(DateTimeKind.Unspecified, unzoned!.Value.Kind);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(9999, 12, 31)]
    public void Should_NotShiftOrOverflow_AtTheRepresentableExtremes(int year, int month, int day)
    {
        var unzoned = WallClock.Unzoned(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(year, month, day), unzoned);
    }
}
