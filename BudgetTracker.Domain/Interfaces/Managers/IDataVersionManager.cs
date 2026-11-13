namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>Serves the per-user data version the client polls to know when to refetch.</summary>
public interface IDataVersionManager
{
    /// <summary>The user's current data version; 0 when the user is not provisioned. Never provisions.</summary>
    /// <param name="cognitoSub">The Cognito subject claim.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The current version.</returns>
    Task<long> GetVersionAsync(string cognitoSub, CancellationToken cancellationToken = default);
}
