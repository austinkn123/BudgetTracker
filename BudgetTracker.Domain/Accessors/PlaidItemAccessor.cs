using System.Security.Cryptography;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;
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
    public const string DataProtectionPurpose = "BudgetTracker.Plaid.AccessToken.v1";

    private readonly BudgetTrackerDbContext _context;
    private readonly IDataProtector _protector;

    public PlaidItemAccessor(BudgetTrackerDbContext context, IDataProtectionProvider dataProtectionProvider)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlaidItem>> GetAllActiveByUserIdAsync(int userId)
    {
        return await _context.PlaidItems
            .AsNoTracking()
            .Include(p => p.Accounts)
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderBy(p => p.Id)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<AccessTokenLookup> GetActiveAccessTokenAsync(int userId, int plaidItemId)
    {
        var encrypted = await _context.PlaidItems
            .AsNoTracking()
            .Where(p => p.Id == plaidItemId && p.UserId == userId && p.IsActive)
            .Select(p => p.AccessTokenEncrypted)
            .FirstOrDefaultAsync();

        return Decrypt(encrypted);
    }

    /// <inheritdoc />
    public async Task<PlaidItem?> GetByPlaidItemIdAsync(string plaidItemId)
    {
        return await _context.PlaidItems
            .AsNoTracking()
            .Include(p => p.Accounts)
            .FirstOrDefaultAsync(p => p.PlaidItemId == plaidItemId && p.IsActive);
    }

    /// <inheritdoc />
    public async Task<AccessTokenLookup> GetAccessTokenByPlaidItemIdAsync(string plaidItemId)
    {
        var encrypted = await _context.PlaidItems
            .AsNoTracking()
            .Where(p => p.PlaidItemId == plaidItemId && p.IsActive)
            .Select(p => p.AccessTokenEncrypted)
            .FirstOrDefaultAsync();

        return Decrypt(encrypted);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlaidItem>> GetAllActiveAsync()
    {
        return await _context.PlaidItems
            .AsNoTracking()
            .Include(p => p.Accounts)
            .Where(p => p.IsActive)
            .ToListAsync();
    }

    /// <summary>
    /// Decrypts a stored token. A <see cref="CryptographicException"/> means the key ring that encrypted it is gone,
    /// which is a recoverable state for the caller (re-link), so it is reported rather than thrown.
    /// </summary>
    private AccessTokenLookup Decrypt(string? encrypted)
    {
        if (encrypted is null)
            return AccessTokenLookup.NotFound;

        try
        {
            return AccessTokenLookup.Found(_protector.Unprotect(encrypted));
        }
        catch (CryptographicException)
        {
            return AccessTokenLookup.Undecryptable;
        }
    }

    /// <inheritdoc />
    public async Task<int> AddAsync(
        int userId,
        string accessTokenPlaintext,
        string plaidItemId,
        string institutionId,
        string institutionName,
        DateTime? consentExpiresAt,
        IReadOnlyList<PlaidAccount> accounts)
    {
        await using var dbTransaction = await _context.Database.BeginTransactionAsync();

        // Plaid reuses account_ids when the same credentials are relinked. Drop snapshot rows left behind by
        // previously unlinked (inactive) items so UQ_PlaidAccounts_PlaidAccountId accepts the reconnect.
        // Snapshots of still-active items are left alone, so linking an already-linked bank again is rejected.
        var incomingAccountIds = accounts.Select(a => a.PlaidAccountId).ToList();
        var staleSnapshots = await _context.PlaidAccounts
            .Where(a => incomingAccountIds.Contains(a.PlaidAccountId) && !a.Item.IsActive)
            .ToListAsync();
        if (staleSnapshots.Count > 0)
        {
            _context.PlaidAccounts.RemoveRange(staleSnapshots);
            await _context.SaveChangesAsync();
        }

        var newItem = new PlaidItem
        {
            UserId = userId,
            PlaidItemId = plaidItemId,
            InstitutionId = institutionId,
            InstitutionName = institutionName,
            AccessTokenEncrypted = _protector.Protect(accessTokenPlaintext),
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
    public async Task<bool> DeactivateAsync(int userId, int plaidItemId)
    {
        var item = await _context.PlaidItems
            .FirstOrDefaultAsync(p => p.Id == plaidItemId && p.UserId == userId && p.IsActive);

        if (item is null)
            return false;

        item.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ReactivateAsync(int userId, int plaidItemId)
    {
        var item = await _context.PlaidItems
            .FirstOrDefaultAsync(p => p.Id == plaidItemId && p.UserId == userId && !p.IsActive);

        if (item is null)
            return false;

        item.IsActive = true;
        await _context.SaveChangesAsync();
        return true;
    }
}
