using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace BudgetTracker.Tests.Accessors;

/// <summary>
/// Makes the next SaveChanges fail the way Npgsql reports a Postgres error, so accessor exception translation can be
/// exercised against the InMemory provider.
/// </summary>
internal sealed class FailingSaveInterceptor : SaveChangesInterceptor
{
    private DbUpdateException? _next;

    /// <summary>Arms the interceptor to throw a wrapped <see cref="PostgresException"/> on the next save.</summary>
    public void FailNextSaveWith(string sqlState, string constraintName) =>
        _next = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            new PostgresException("duplicate key value", "ERROR", "ERROR", sqlState, constraintName: constraintName));

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (_next is { } failure)
        {
            _next = null;
            throw failure;
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
