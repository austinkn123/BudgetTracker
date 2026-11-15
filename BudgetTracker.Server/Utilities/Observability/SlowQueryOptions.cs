namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>Settings for <see cref="SlowQueryInterceptor"/>, bound from <c>Observability:SlowQuery</c>.</summary>
public sealed class SlowQueryOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Observability:SlowQuery";

    /// <summary>A database command that takes at least this long is logged as a warning.</summary>
    public int ThresholdMs { get; set; } = 500;
}
