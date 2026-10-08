using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Engines;

public interface IBudgetPlanEngine
{
    string? ValidateBudgetPlan(BudgetPlan budgetPlan);
    void NormalizeForPersistence(BudgetPlan budgetPlan);

    /// <summary>
    /// Checks that <paramref name="name"/> does not match any of the user's other plan names for the same month,
    /// ignoring case, mirroring the unique index on ("UserId", "PlanMonth", lower("Name")).
    /// </summary>
    /// <param name="name">The candidate plan name.</param>
    /// <param name="existingNames">Names of the user's other plans for the same month (excluding the one being updated).</param>
    /// <returns>An error message when the name is taken; otherwise null.</returns>
    string? ValidateNameIsUnique(string name, IEnumerable<string> existingNames);

    /// <summary>The message shown when <paramref name="name"/> is already taken, used when the database catches a race.</summary>
    /// <param name="name">The candidate plan name.</param>
    /// <returns>The same message <see cref="ValidateNameIsUnique"/> returns for a taken name.</returns>
    string DuplicateNameError(string name);
}