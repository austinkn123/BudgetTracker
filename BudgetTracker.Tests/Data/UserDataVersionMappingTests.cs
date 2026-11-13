using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BudgetTracker.Tests.Data;

/// <summary>
/// Pins the <see cref="User.DataVersion"/> mapping: a plain NOT NULL bigint counter bumped by an atomic
/// <c>UPDATE ... SET "DataVersion" = "DataVersion" + 1</c>. It must never become a concurrency token (that would make every
/// tracked User save fail once a bump lands) and must stay unindexed so the bump remains a heap-only-tuple update.
/// Built against the Npgsql provider without opening a connection, like <see cref="WebullModelMappingTests"/>.
/// </summary>
public class UserDataVersionMappingTests
{
    private static readonly IModel Model = BuildModel();

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only")
            .Options;
        using var context = new BudgetTrackerDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static IProperty DataVersion() =>
        Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.DataVersion))!;

    [Fact]
    public void DataVersion_IsNotNullBigint_WithNoModelDefault()
    {
        var column = DataVersion();

        Assert.Equal("bigint", column.GetColumnType());
        Assert.Equal("DataVersion", column.GetColumnName());
        Assert.False(column.IsNullable);
        // GetDefaultValue() echoes the CLR default for non-nullable columns; check the annotation itself so a
        // HasDefaultValue(...) on the property (sentinel warning) is what this catches.
        Assert.Null(column.FindAnnotation(RelationalAnnotationNames.DefaultValue));
        Assert.Null(column.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.Never, column.ValueGenerated);
    }

    [Fact]
    public void DataVersion_IsNeverAConcurrencyToken()
    {
        var column = DataVersion();

        Assert.False(column.IsConcurrencyToken);
        Assert.DoesNotContain(Model.FindEntityType(typeof(User))!.GetProperties(), p => p.IsConcurrencyToken);
    }

    [Fact]
    public void DataVersion_IsNotIndexed()
    {
        Assert.DoesNotContain(
            Model.FindEntityType(typeof(User))!.GetIndexes(),
            i => i.Properties.Any(p => p.Name == nameof(User.DataVersion)));
    }
}
