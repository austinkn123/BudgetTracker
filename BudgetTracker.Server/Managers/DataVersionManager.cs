using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Managers;

namespace BudgetTracker.Server.Managers;

/// <summary>Serves the polled data version without provisioning: an unknown user simply reads as version 0.</summary>
public class DataVersionManager(IDataVersionAccessor accessor) : IDataVersionManager
{
    /// <inheritdoc />
    public async Task<long> GetVersionAsync(string cognitoSub, CancellationToken cancellationToken = default) =>
        await accessor.GetByCognitoSubAsync(cognitoSub, cancellationToken) ?? 0;
}
