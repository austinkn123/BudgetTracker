namespace BudgetTracker.Domain.Models;

/// <summary>
/// One position in a <see cref="WebullAccount"/> as of the last fetch, stored as Webull sent it. Replaced wholesale on every
/// fetch. Named apart from the <c>Webull.WebullPosition</c> contract record to avoid an ambiguous reference.
/// </summary>
public class WebullAccountPosition
{
    public int Id { get; set; }
    public int WebullAccountId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string InstrumentType { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal? LastPrice { get; set; }

    /// <summary>Webull's raw <c>cost_price</c>; what it means is decided by the holdings engine, not here.</summary>
    public decimal? CostPrice { get; set; }

    public decimal? UnrealizedProfitLoss { get; set; }
    public string Currency { get; set; } = string.Empty;
}
