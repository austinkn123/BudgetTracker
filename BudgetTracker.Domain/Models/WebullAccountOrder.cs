namespace BudgetTracker.Domain.Models;

/// <summary>
/// One Webull order exactly as order history reported it (<see cref="Webull.WebullOrder"/>), in any status. History, so it
/// is keyed by (connection, <see cref="AccountId"/>, <see cref="OrderId"/>) with no foreign key to <see cref="WebullAccount"/>:
/// replacing the balance snapshot never deletes it. A re-fetch overwrites the row. Times are UTC.
/// </summary>
public class WebullAccountOrder
{
    public int WebullConnectionId { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string? InstrumentType { get; set; }
    public string Side { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? FilledQuantity { get; set; }
    public decimal? FilledPrice { get; set; }
    public DateTime PlacedAt { get; set; }
    public DateTime? FilledAt { get; set; }
}
