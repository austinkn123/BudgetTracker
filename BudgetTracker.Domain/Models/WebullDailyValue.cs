namespace BudgetTracker.Domain.Models;

/// <summary>
/// One account's value for one US-Eastern date: the last successful fetch of that day wins. History, so it is keyed by
/// <see cref="AccountId"/> with no foreign key to <see cref="WebullAccount"/>: replacing the snapshot never deletes it.
/// </summary>
public class WebullDailyValue
{
    public int WebullConnectionId { get; set; }
    public DateOnly EasternDate { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public decimal TotalValue { get; set; }
    public decimal? Cash { get; set; }
    public decimal? MarketValue { get; set; }
    public decimal? DayProfitLoss { get; set; }
    public decimal? UnrealizedProfitLoss { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; }
}
