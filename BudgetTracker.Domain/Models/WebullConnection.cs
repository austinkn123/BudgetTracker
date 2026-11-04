using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Domain.Models;

/// <summary>
/// The user's Webull link: the encrypted access token and its status, the last balance fetch, and the last error.
/// One row per user. The token columns are all set or all null (a row can exist without a token, to record an error).
/// </summary>
public class WebullConnection
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>The Webull access token encrypted via ASP.NET Core Data Protection. Never logged. Never returned to the client.</summary>
    public string? AccessTokenEncrypted { get; set; }

    public WebullTokenStatus? TokenStatus { get; set; }
    public DateTime? TokenCreatedAt { get; set; }
    public DateTime? LastFetchedAt { get; set; }
    public WebullErrorKind? LastError { get; set; }
    public ICollection<WebullAccount> Accounts { get; set; } = [];

    /// <summary>Daily value history. Never Include this; rows are only ever added through it, so a new connection's id flows in.</summary>
    public ICollection<WebullDailyValue> DailyValues { get; set; } = [];
}
