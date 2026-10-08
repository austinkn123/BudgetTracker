namespace BudgetTracker.Domain.Common;

/// <summary>
/// Thrown by an Accessor when the database rejects a write because a per-user name uniqueness index was violated,
/// typically by a concurrent request that passed the same pre-check. Lets Managers return the friendly duplicate-name
/// message without referencing the database provider.
/// </summary>
public sealed class UniqueNameViolationException : Exception
{
    /// <summary>Creates the exception for the violated index.</summary>
    /// <param name="constraintName">The name of the violated unique index.</param>
    /// <param name="innerException">The provider exception that reported the violation.</param>
    public UniqueNameViolationException(string constraintName, Exception innerException)
        : base($"Unique name index \"{constraintName}\" was violated.", innerException)
    {
        ConstraintName = constraintName;
    }

    /// <summary>The name of the violated unique index.</summary>
    public string ConstraintName { get; }
}
