namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>Per-user live-data change marker (Users.DataVersion). Owns only that column.</summary>
public interface IDataVersionAccessor
{
    /// <summary>
    /// Atomic <c>UPDATE … SET "DataVersion" = "DataVersion" + 1</c> for the user, run as its own statement after the
    /// caller's writes have committed. Best-effort: never throws; logs and returns false on failure or unknown user.
    /// Takes no CancellationToken on purpose: a client abort after the write must still bump.
    /// </summary>
    /// <param name="userId">The internal user id whose data changed.</param>
    /// <returns>True when exactly one row was bumped.</returns>
    Task<bool> BumpAsync(int userId);

    /// <summary>The user's DataVersion by Cognito sub (AsNoTracking projection, one indexed read), or null when no row.</summary>
    /// <param name="cognitoSub">The Cognito subject claim.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The current version, or null when the user is not provisioned.</returns>
    Task<long?> GetByCognitoSubAsync(string cognitoSub, CancellationToken cancellationToken = default);
}
