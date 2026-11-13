namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>The log category for security signals: failed authentication and denied (401/403) requests.</summary>
public static class SecurityLog
{
    /// <summary>The logger category name, so CloudWatch queries can filter on it.</summary>
    public const string Category = "BudgetTracker.Security";
}
