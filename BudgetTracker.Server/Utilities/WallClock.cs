namespace BudgetTracker.Server.Utilities;

/// <summary>
/// Normalises bound query-string dates for the ledger's <c>timestamp without time zone</c> columns. A zoned input
/// ("…Z" binds as <see cref="DateTimeKind.Utc"/>) is rejected by Npgsql against those columns, so the kind is dropped
/// and the wall-clock reading kept as given — no time-zone conversion.
/// </summary>
public static class WallClock
{
    /// <summary>The same wall-clock reading with <see cref="DateTimeKind.Unspecified"/>.</summary>
    /// <param name="value">A bound date of any kind.</param>
    /// <returns>The unzoned value.</returns>
    public static DateTime Unzoned(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    /// <summary>The same wall-clock reading with <see cref="DateTimeKind.Unspecified"/>, or null when absent.</summary>
    /// <param name="value">A bound optional date of any kind.</param>
    /// <returns>The unzoned value, or null.</returns>
    public static DateTime? Unzoned(DateTime? value) => value is DateTime v ? Unzoned(v) : null;
}
