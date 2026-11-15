namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>Answers whether the database can be reached right now. Reads no table and no user data.</summary>
public interface IDatabaseHealthAccessor
{
    /// <summary>Tries to open a connection. Never throws for an unreachable database.</summary>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns><c>true</c> when the database answered; otherwise <c>false</c>.</returns>
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
