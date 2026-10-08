using BudgetTracker.Domain.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BudgetTracker.Tests.Data;

/// <summary>
/// Npgsql maps <see cref="DateTime"/> to <c>timestamp with time zone</c> by default and throws on non-UTC writes to it.
/// The app keeps SQL Server <c>datetime2</c> semantics instead, so every DateTime column, nullable or not, must use
/// <see cref="UnspecifiedKindDateTimeConverter"/> and be <c>timestamp without time zone</c> (bar the listed calendar-date
/// columns). Built against the Npgsql
/// provider (no connection is opened) so the provider's own conventions are part of what is checked.
/// </summary>
public class DateTimeColumnMappingTests
{
    private static IReadOnlyList<IProperty> DateTimeProperties()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only")
            .Options;
        using var context = new BudgetTrackerDbContext(options);
        var model = context.GetService<IDesignTimeModel>().Model;

        return model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(DateTime))
            .ToList();
    }

    /// <summary>
    /// Columns deliberately mapped to a calendar <c>date</c> (carried over from SQL Server). They have no time or zone
    /// component, so the timestamp rule does not apply; anything else must be listed here consciously.
    /// </summary>
    private static readonly HashSet<string> DateOnlyColumns = ["BudgetPlan.PlanMonth"];

    private static string NameOf(IProperty p) => $"{p.DeclaringType.DisplayName()}.{p.Name}";

    [Fact]
    public void Should_MapEveryDateTimeAndNullableDateTimeToTimestampWithoutTimeZone_When_ModelIsBuilt()
    {
        var properties = DateTimeProperties().Where(p => !DateOnlyColumns.Contains(NameOf(p))).ToList();

        Assert.Contains(properties, p => p.ClrType == typeof(DateTime));
        Assert.Contains(properties, p => p.ClrType == typeof(DateTime?));
        Assert.All(properties, p => Assert.True(
            p.GetColumnType() == "timestamp without time zone",
            $"{NameOf(p)} is mapped to {p.GetColumnType()}"));
    }

    [Fact]
    public void Should_MapExemptDateOnlyColumnsToDate_When_ModelIsBuilt()
    {
        var exempt = DateTimeProperties().Where(p => DateOnlyColumns.Contains(NameOf(p))).ToList();

        Assert.Equal(DateOnlyColumns.Order(), exempt.Select(NameOf).Order());
        Assert.All(exempt, p => Assert.Equal("date", p.GetColumnType()));
    }

    [Fact]
    public void Should_UseUnspecifiedKindConverterOnEveryDateTimeAndNullableDateTime_When_ModelIsBuilt()
    {
        var properties = DateTimeProperties();

        Assert.NotEmpty(properties);
        Assert.All(properties, p => Assert.True(
            p.GetValueConverter() is UnspecifiedKindDateTimeConverter,
            $"{NameOf(p)} uses {p.GetValueConverter()?.GetType().Name ?? "no converter"}"));
    }
}
