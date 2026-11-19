namespace BudgetTracker.Domain.Common;

/// <summary>
/// The user's calendar date. Transactions are stored as timezone-free calendar dates, so "today" has to be the user's
/// date too: the server's UTC date is already tomorrow every US evening, which put pacing a day ahead and moved
/// Money Map and cash-flow cut-offs.
/// </summary>
public static class LocalToday
{
    /// <summary>
    /// The client's date when it is within a day of the UTC date (every real time zone is), otherwise the UTC date.
    /// The bound keeps a wrong or forged value from shifting what a request reads.
    /// </summary>
    /// <param name="clientToday">The date the browser sent, or null when it sent none.</param>
    /// <param name="utcNow">The current UTC instant.</param>
    /// <returns>A date at midnight with <see cref="DateTimeKind.Unspecified"/>, matching stored transaction dates.</returns>
    public static DateTime Resolve(DateTime? clientToday, DateTime utcNow)
    {
        var utcToday = DateTime.SpecifyKind(utcNow.Date, DateTimeKind.Unspecified);
        if (clientToday is not DateTime client)
            return utcToday;

        var clientDate = DateTime.SpecifyKind(client.Date, DateTimeKind.Unspecified);
        return Math.Abs((clientDate - utcToday).TotalDays) <= 1 ? clientDate : utcToday;
    }
}
