using Microsoft.Extensions.Logging;

namespace BudgetTracker.Domain.Common;

/// <summary>Source-generated log messages for data access. Event IDs 4000–4999.</summary>
public static partial class DataLog
{
    /// <summary>Bumping a user's data version failed; clients catch up on the next change.</summary>
    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "Bumping the data version for user {UserId} failed; clients refresh on the next change.")]
    public static partial void DataVersionBumpFailed(this ILogger logger, Exception exception, int userId);
}
