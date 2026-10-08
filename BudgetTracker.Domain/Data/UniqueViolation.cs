using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BudgetTracker.Domain.Data;

/// <summary>
/// Recognises Postgres unique-index violations surfaced through EF Core, so Accessors can translate only the ones they own.
/// </summary>
internal static class UniqueViolation
{
    /// <summary>Name of the case-insensitive (UserId, lower(Name)) index on Categories.</summary>
    public const string CategoryName = "UQ_Categories_User_Name";

    /// <summary>Name of the case-insensitive (UserId, PlanMonth, lower(Name)) index on BudgetPlans.</summary>
    public const string BudgetPlanName = "UQ_BudgetPlans_User_Month_Name";

    /// <summary>Name of the filtered unique index on Users.CognitoSub.</summary>
    public const string UserCognitoSub = "IX_Users_CognitoSub";

    /// <summary>Whether the save failed because the given unique index was violated.</summary>
    /// <param name="exception">The exception thrown by SaveChanges.</param>
    /// <param name="constraintName">The index to match; other unique indexes are not matched.</param>
    /// <returns>True only for SqlState 23505 on <paramref name="constraintName"/>.</returns>
    public static bool IsUniqueViolationOf(this DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName == constraintName;
}
