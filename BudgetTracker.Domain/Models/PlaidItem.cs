namespace BudgetTracker.Domain.Models;

/// <summary>
/// Represents an active Plaid "Item" — the link between a BudgetTracker user and a single financial institution.
/// One active row per user is enforced at the DB layer; soft-delete (<see cref="IsActive"/>) keeps history for audit.
/// </summary>
public class PlaidItem
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>Plaid's opaque item identifier (unique across all Plaid items globally).</summary>
    public string PlaidItemId { get; set; } = string.Empty;

    public string InstitutionId { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;

    /// <summary>The Plaid access_token encrypted via ASP.NET Core Data Protection. Never logged. Never returned to the client.</summary>
    public string AccessTokenEncrypted { get; set; } = string.Empty;

    /// <summary>Cursor returned by <c>/transactions/sync</c>; sent back on the next sync to fetch only deltas.</summary>
    public string? SyncCursor { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? ConsentExpiresAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<PlaidAccount> Accounts { get; set; } = [];
}
