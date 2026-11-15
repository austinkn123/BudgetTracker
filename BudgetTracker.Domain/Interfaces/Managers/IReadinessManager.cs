namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>
/// Answers whether the app's dependencies are reachable, for the on-demand readiness check. Calling it wakes the
/// database, so it must never be polled (project-docs/observability.md §4).
/// </summary>
public interface IReadinessManager
{
    /// <summary>Checks the dependencies the app needs to serve requests.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><c>true</c> when every dependency answered; otherwise <c>false</c>.</returns>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);
}
