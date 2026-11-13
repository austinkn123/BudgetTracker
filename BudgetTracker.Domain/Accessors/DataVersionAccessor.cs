using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BudgetTracker.Domain.Accessors;

/// <summary>Reads and bumps <c>Users.DataVersion</c>; touches no other column.</summary>
public class DataVersionAccessor(BudgetTrackerDbContext context, ILogger<DataVersionAccessor> logger) : IDataVersionAccessor
{
    /// <inheritdoc />
    public async Task<bool> BumpAsync(int userId)
    {
        try
        {
            // A single server-side increment: loading and saving the entity would lose concurrent bumps.
            var rows = await context.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.DataVersion, u => u.DataVersion + 1));
            return rows == 1;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bumping the data version for user {UserId} failed; clients refresh on the next change.", userId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<long?> GetByCognitoSubAsync(string cognitoSub, CancellationToken cancellationToken = default) =>
        await context.Users
            .AsNoTracking()
            .Where(u => u.CognitoSub == cognitoSub)
            .Select(u => (long?)u.DataVersion)
            .FirstOrDefaultAsync(cancellationToken);
}
