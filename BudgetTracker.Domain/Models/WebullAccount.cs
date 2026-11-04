namespace BudgetTracker.Domain.Models;

/// <summary>One Webull account's balance as of <see cref="WebullConnection.LastFetchedAt"/>. Read-only data; never budget data.</summary>
public class WebullAccount
{
    public int Id { get; set; }
    public int WebullConnectionId { get; set; }

    /// <summary>Webull's <c>account_id</c>; unique within its connection.</summary>
    public string AccountId { get; set; } = string.Empty;

    public string? AccountLabel { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public decimal TotalNetLiquidationValue { get; set; }
    public decimal? TotalCashBalance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? TotalMarketValue { get; set; }
    public decimal? TotalDayProfitLoss { get; set; }
    public decimal? TotalUnrealizedProfitLoss { get; set; }
    public ICollection<WebullAccountPosition> Positions { get; set; } = [];
}
