using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BudgetTracker.Domain.Data;

/// <summary>
/// Recognises Postgres foreign-key violations surfaced through EF Core, so Accessors can translate only the ones they own.
/// </summary>
internal static class ForeignKeyViolation
{
    /// <summary>Foreign key from Transactions.CategoryId onto Categories.</summary>
    public const string TransactionCategory = "FK_Transactions_Categories_CategoryId";

    /// <summary>Foreign key from BudgetPlanEntries.CategoryId onto Categories.</summary>
    public const string BudgetPlanEntryCategory = "FK_BudgetPlanEntries_Categories_CategoryId";

    /// <summary>
    /// Every foreign key onto Categories. A new FK onto Categories must be added here, or deleting a category it
    /// references surfaces as an unhandled error instead of the "in use" message.
    /// </summary>
    public static readonly string[] OntoCategories = [TransactionCategory, BudgetPlanEntryCategory];

    /// <summary>Whether the save failed because one of the given foreign keys was violated.</summary>
    /// <param name="exception">The exception thrown by SaveChanges.</param>
    /// <param name="constraintNames">The foreign keys to match; other foreign keys are not matched.</param>
    /// <returns>True only for SqlState 23503 on one of <paramref name="constraintNames"/>.</returns>
    public static bool IsForeignKeyViolationOf(this DbUpdateException exception, params string[] constraintNames) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } pg
        && constraintNames.Contains(pg.ConstraintName);
}
