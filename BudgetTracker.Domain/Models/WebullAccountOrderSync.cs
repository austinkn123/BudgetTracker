using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Models;

/// <summary>
/// How much of one account's order history is saved (<see cref="WebullOrderSync"/>), with the window being paged through
/// flattened into <see cref="WindowStart"/>, <see cref="WindowEnd"/> and <see cref="PaginationKey"/>. Keyed by connection and
/// <see cref="AccountId"/> with no foreign key to <see cref="WebullAccount"/>. Times are UTC.
/// </summary>
public class WebullAccountOrderSync
{
    public int WebullConnectionId { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public DateTime? CoveredFrom { get; set; }
    public DateTime? CoveredTo { get; set; }
    public DateTime? WindowStart { get; set; }
    public DateTime? WindowEnd { get; set; }
    public string? PaginationKey { get; set; }
    public WebullErrorKind? LastError { get; set; }
}
