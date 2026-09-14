using BudgetTracker.Domain.Common;

namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>
/// Development-only orchestration that replaces the Plaid Sandbox's stock fixture data with
/// transactions derived from the user's own budget plan (BUD-3 dev tooling).
///
/// Plaid's default Sandbox user returns "Tartan Bank" fixtures whose amounts bear no relation to a
/// real budget, which makes every figure on the dashboard nonsense. This seeds a
/// <c>user_custom</c> item instead, so actuals reconcile against the plan.
/// </summary>
public interface ISandboxSeedManager
{
    /// <summary>
    /// Build a custom Sandbox item for the user and run it through the normal exchange + sync path,
    /// so the seeded data arrives via exactly the same code as a real link.
    /// </summary>
    /// <param name="userId">Owner of the plan the seed is derived from.</param>
    /// <param name="months">How many whole months back from today to generate, inclusive of this one.</param>
    Task<Result<PlaidSyncSummary>> SeedAsync(int userId, int months, CancellationToken cancellationToken = default);
}
