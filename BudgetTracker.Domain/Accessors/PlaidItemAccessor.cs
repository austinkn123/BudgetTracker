using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

/// <summary>
/// Persists and reads <see cref="PlaidItem"/> rows. Owns access_token encryption via
/// ASP.NET Core Data Protection so callers never handle plaintext tokens after persistence.
/// </summary>
public class PlaidItemAccessor : IPlaidItemAccessor
{
    /// <summary>Data Protection purpose string — change this and you invalidate previously-stored tokens.</summary>
    internal const string DataProtectionPurpose = "BudgetTracker.Plaid.AccessToken.v1";

    private readonly BudgetTrackerDbContext _context;
    private readonly IDataProtector _protector;

    public PlaidItemAccessor(BudgetTrackerDbContext context, IDataProtectionProvider dataProtectionProvider)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
    }

    /// <inheritdoc />
    public async Task<PlaidItem?> GetActiveByUserIdAsync(int userId)
    {
        return await _context.PlaidItems
            .AsNoTracking()
            .Include(p => p.Accounts)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);
    }

    /// <inheritdoc />
    public async Task<string?> GetActiveAccessTokenAsync(int userId)
    {
        var encrypted = await _context.PlaidItems
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive)
            .Select(p => p.AccessTokenEncrypted)
            .FirstOrDefaultAsync();

        return encrypted is null ? null : _protector.Unprotect(encrypted);
    }

    /// <inheritdoc />
    public async Task<int> ReplaceActiveAsync(
        int userId,
        string accessTokenPlaintext,
        string plaidItemId,
        string institutionId,
        string institutionName,
        DateTime? consentExpiresAt,
        IReadOnlyList<PlaidAccount> accounts)
    {
        // Atomic replace: deactivate any current active item, insert new — single transaction so the
        // filtered unique index UQ_PlaidItems_UserId_Active never sees two active rows for one user.
        await using var dbTransaction = await _context.Database.BeginTransactionAsync();

        var existing = await _context.PlaidItems
            .Include(p => p.Accounts)
            .Where(p => p.UserId == userId && p.IsActive)
            .ToListAsync();
        foreach (var item in existing)
        {
            item.IsActive = false;
            // Remove the old snapshot rows so UQ_PlaidAccounts_PlaidAccountId doesn't reject a
            // same-institution reconnect (Plaid reuses account_ids for the same credentials).
            _context.PlaidAccounts.RemoveRange(item.Accounts);
        }
        await _context.SaveChangesAsync();

        var encryptedToken = _protector.Protect(accessTokenPlaintext);

        var newItem = new PlaidItem
        {
            UserId = userId,
            PlaidItemId = plaidItemId,
            InstitutionId = institutionId,
            InstitutionName = institutionName,
            AccessTokenEncrypted = encryptedToken,
            IsActive = true,
            ConsentExpiresAt = consentExpiresAt,
            Accounts = accounts.ToList()
        };

        _context.PlaidItems.Add(newItem);
        await _context.SaveChangesAsync();

        await dbTransaction.CommitAsync();
        return newItem.Id;
    }

    /// <inheritdoc />
    public async Task UpdateSyncStateAsync(int plaidItemId, string nextCursor, DateTime lastSyncedAt)
    {
        var item = await _context.PlaidItems.FirstOrDefaultAsync(p => p.Id == plaidItemId);
        if (item is null)
            return;

        item.SyncCursor = nextCursor;
        item.LastSyncedAt = lastSyncedAt;
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<bool> DeactivateActiveAsync(int userId)
    {
        var active = await _context.PlaidItems
            .Where(p => p.UserId == userId && p.IsActive)
            .ToListAsync();

        if (active.Count == 0)
            return false;

        foreach (var item in active)
        {
            item.IsActive = false;
        }
        await _context.SaveChangesAsync();
        return true;
    }
}
