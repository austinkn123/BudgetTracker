using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Managers;

namespace BudgetTracker.Server.Managers;

/// <summary>Readiness is the database answering; it is the only dependency every request needs.</summary>
public class ReadinessManager(IDatabaseHealthAccessor database) : IReadinessManager
{
    /// <inheritdoc />
    public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default) =>
        database.CanConnectAsync(cancellationToken);
}
