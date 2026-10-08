using BudgetTracker.Domain.Data;

namespace BudgetTracker.Tests.Data;

/// <summary>
/// Npgsql rejects UTC-kind values for <c>timestamp without time zone</c> columns. The converter keeps
/// SQL Server datetime2 semantics: the wall-clock value is stored as-is and the Kind is dropped.
/// </summary>
public class UnspecifiedKindDateTimeConverterTests
{
    private readonly UnspecifiedKindDateTimeConverter _sut = new();

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void ToProvider_AnyKind_KeepsTicksAndReturnsUnspecified(DateTimeKind kind)
    {
        var value = new DateTime(2026, 9, 27, 14, 30, 15, kind).AddTicks(7);

        var stored = (DateTime)_sut.ConvertToProvider(value)!;

        Assert.Equal(DateTimeKind.Unspecified, stored.Kind);
        Assert.Equal(value.Ticks, stored.Ticks);
    }

    [Fact]
    public void FromProvider_ReturnsSameWallClockValueAsUnspecified()
    {
        var stored = new DateTime(2026, 9, 27, 14, 30, 15, DateTimeKind.Unspecified);

        var read = (DateTime)_sut.ConvertFromProvider(stored)!;

        Assert.Equal(stored, read);
        Assert.Equal(DateTimeKind.Unspecified, read.Kind);
    }

    [Fact]
    public void ToProvider_MinAndMaxValues_RoundTrip()
    {
        Assert.Equal(DateTime.MinValue.Ticks, ((DateTime)_sut.ConvertToProvider(DateTime.MinValue)!).Ticks);
        Assert.Equal(DateTime.MaxValue.Ticks, ((DateTime)_sut.ConvertToProvider(DateTime.MaxValue)!).Ticks);
    }
}
