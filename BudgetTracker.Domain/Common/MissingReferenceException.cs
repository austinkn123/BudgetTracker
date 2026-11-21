namespace BudgetTracker.Domain.Common;

/// <summary>
/// Thrown by an Accessor when a write points at a row that no longer exists (a foreign-key violation), typically
/// because a concurrent request deleted it after the Manager's check. Lets Managers answer "not found" without
/// referencing the database provider.
/// </summary>
public sealed class MissingReferenceException(string constraintName, Exception innerException)
    : Exception($"Foreign key \"{constraintName}\" points at a missing row.", innerException)
{
    /// <summary>The violated foreign key.</summary>
    public string ConstraintName { get; } = constraintName;
}
