namespace BudgetTracker.Domain.Common;

/// <summary>
/// Thrown by an Accessor when the database rejects a delete because other rows still reference the entity through a
/// foreign key. Lets Managers return the friendly "in use" message without referencing the database provider.
/// </summary>
public sealed class EntityInUseException : Exception
{
    /// <summary>Creates the exception for the violated foreign key.</summary>
    /// <param name="constraintName">The name of the violated foreign key constraint.</param>
    /// <param name="innerException">The provider exception that reported the violation.</param>
    public EntityInUseException(string constraintName, Exception innerException)
        : base($"Foreign key \"{constraintName}\" still references the entity.", innerException)
    {
        ConstraintName = constraintName;
    }

    /// <summary>The name of the violated foreign key constraint.</summary>
    public string ConstraintName { get; }
}
