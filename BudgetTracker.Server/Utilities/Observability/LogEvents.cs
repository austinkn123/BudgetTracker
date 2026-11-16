namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>
/// The event-ID ranges for source-generated log messages, one block per area, so a CloudWatch query can filter on a
/// stable <c>EventId</c> instead of message text. IDs are never reused or renumbered: retire an ID with its message.
/// </summary>
public static class LogEvents
{
    /// <summary>Observability and security messages: 1000–1999 (<see cref="ObservabilityLog"/>).</summary>
    public const int ObservabilityBase = 1000;

    /// <summary>Plaid link, sync and sandbox messages: 2000–2999 (<see cref="PlaidLog"/>).</summary>
    public const int PlaidBase = 2000;

    /// <summary>Webull messages: 3000–3999 (<see cref="WebullLog"/>).</summary>
    public const int WebullBase = 3000;

    /// <summary>Data-access messages: 4000–4999 (<c>BudgetTracker.Domain.Common.DataLog</c>).</summary>
    public const int DataBase = 4000;
}
