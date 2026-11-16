using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>
/// Pure rules for deriving a Plaid Sandbox <c>user_custom</c> seed from the user's budget plan (BUD-3 dev tooling).
/// </summary>
public interface ISandboxSeedEngine
{
    /// <summary>Checks the requested seed length.</summary>
    /// <param name="months">Whole months back from today to generate, inclusive of this one.</param>
    /// <returns>An error message when <paramref name="months"/> is outside 1–12; otherwise null.</returns>
    string? ValidateMonths(int months);

    /// <summary>
    /// One seed line per positive expense entry of <paramref name="plan"/>, with the category resolved to a
    /// realistic merchant and transaction count.
    /// </summary>
    /// <param name="plan">The plan the seed is derived from.</param>
    /// <param name="categoryNames">The user's category names by id; entries without a known category read "General".</param>
    /// <returns>The seed lines, in entry order.</returns>
    IReadOnlyList<SandboxSeedLine> BuildLines(BudgetPlan plan, IReadOnlyDictionary<int, string> categoryNames);

    /// <summary>The <c>override_password</c> configuration for <c>/sandbox/public_token/create</c>.</summary>
    /// <param name="lines">The expense lines to emit each month.</param>
    /// <param name="monthlyIncome">Net income paid in two halves each month.</param>
    /// <param name="from">First date to emit.</param>
    /// <param name="through">Last date to emit.</param>
    /// <returns>The serialized configuration.</returns>
    string BuildConfig(IReadOnlyList<SandboxSeedLine> lines, decimal monthlyIncome, DateOnly from, DateOnly through);

    /// <summary>
    /// The active item a new seed replaces: the most recently created (previous seed or stock sandbox link), ties
    /// broken by the highest id. Users may link many banks, so only this one is retired.
    /// </summary>
    /// <param name="activeItems">The user's active items, captured before the new seed is exchanged.</param>
    /// <returns>The item to replace, or null when the user has none.</returns>
    PlaidItem? SelectItemToReplace(IReadOnlyList<PlaidItem> activeItems);
}
