using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;

namespace BudgetTracker.Domain.Accessors;

/// <summary>Connection-level database probe: opens a connection, runs no query against the schema.</summary>
public class DatabaseHealthAccessor(BudgetTrackerDbContext context) : IDatabaseHealthAccessor
{
    /// <inheritdoc />
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) =>
        context.Database.CanConnectAsync(cancellationToken);
}
