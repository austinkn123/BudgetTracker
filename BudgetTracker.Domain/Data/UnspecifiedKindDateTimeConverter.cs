using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BudgetTracker.Domain.Data;

/// <summary>
/// Stores <see cref="DateTime"/> values as-is with <see cref="DateTimeKind.Unspecified"/>, matching the
/// SQL Server <c>datetime2</c> behaviour the app was written against. Applied model-wide by
/// <see cref="BudgetTrackerDbContext"/> so Npgsql never sees a UTC/Local kind on a
/// <c>timestamp without time zone</c> column.
/// </summary>
public class UnspecifiedKindDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(
        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified));
