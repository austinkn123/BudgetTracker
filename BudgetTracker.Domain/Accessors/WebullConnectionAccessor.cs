using System.Security.Cryptography;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;
using BudgetTracker.Domain.Webull;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

/// <summary>
/// Persists and reads the user's <see cref="WebullConnection"/> and its <see cref="WebullAccount"/> rows. Owns access-token
/// encryption via ASP.NET Core Data Protection under its own purpose, so callers never store a plaintext token.
/// Every write creates the row when missing.
/// </summary>
public class WebullConnectionAccessor : IWebullConnectionAccessor
{
    /// <summary>Data Protection purpose string. Change it and every stored Webull token becomes undecryptable.</summary>
    public const string DataProtectionPurpose = "BudgetTracker.Webull.AccessToken.v1";

    private readonly BudgetTrackerDbContext _context;
    private readonly IDataProtector _protector;

    /// <summary>Initializes the accessor.</summary>
    /// <param name="context">The scoped DbContext.</param>
    /// <param name="dataProtectionProvider">Supplies the protector for <see cref="DataProtectionPurpose"/>.</param>
    public WebullConnectionAccessor(BudgetTrackerDbContext context, IDataProtectionProvider dataProtectionProvider)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
    }

    /// <inheritdoc />
    public async Task<WebullSnapshot?> GetAsync(int userId)
    {
        var connection = await _context.WebullConnections
            .AsNoTracking()
            .Include(c => c.Accounts)
            .FirstOrDefaultAsync(c => c.UserId == userId);
        if (connection is null)
            return null;

        // Columns are timestamp without time zone holding UTC; restore the kind so they serialize with a Z.
        return new WebullSnapshot(
            connection.TokenStatus,
            AsUtc(connection.TokenCreatedAt),
            AsUtc(connection.LastFetchedAt),
            connection.LastError,
            connection.Accounts.OrderBy(a => a.Id).Select(ToBalance).ToList());
    }

    /// <inheritdoc />
    public async Task<AccessTokenLookup> GetAccessTokenAsync(int userId)
    {
        var encrypted = await _context.WebullConnections
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => c.AccessTokenEncrypted)
            .FirstOrDefaultAsync();
        if (encrypted is null)
            return AccessTokenLookup.NotFound;

        try
        {
            return AccessTokenLookup.Found(_protector.Unprotect(encrypted));
        }
        catch (CryptographicException)
        {
            // The key ring that encrypted it is gone; the user must verify again.
            return AccessTokenLookup.Undecryptable;
        }
    }

    /// <inheritdoc />
    public async Task SaveTokenAsync(int userId, string accessTokenPlaintext, WebullTokenStatus status, DateTime createdAt)
    {
        var connection = await GetOrCreateAsync(userId);
        connection.AccessTokenEncrypted = _protector.Protect(accessTokenPlaintext);
        connection.TokenStatus = status;
        connection.TokenCreatedAt = createdAt;
        connection.LastError = null;
        await SaveOrDiscardAsync();
    }

    /// <inheritdoc />
    public async Task SetTokenStatusAsync(int userId, WebullTokenStatus status)
    {
        var connection = await _context.WebullConnections
            .FirstOrDefaultAsync(c => c.UserId == userId && c.AccessTokenEncrypted != null);
        if (connection is null)
            return;

        connection.TokenStatus = status;
        connection.LastError = null;
        await SaveOrDiscardAsync();
    }

    /// <inheritdoc />
    public async Task SetLastErrorAsync(int userId, WebullErrorKind error)
    {
        var connection = await GetOrCreateAsync(userId);
        connection.LastError = error;
        await SaveOrDiscardAsync();
    }

    /// <summary>
    /// Saves, or on failure discards everything tracked and rethrows. The close run walks every user through one scoped
    /// context, so a failed save must not leave its changes behind for the next user's save to persist or trip over.
    /// </summary>
    private async Task SaveOrDiscardAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<WebullConnection> GetOrCreateAsync(int userId)
    {
        var connection = await _context.WebullConnections
            .Include(c => c.Accounts)
            .ThenInclude(a => a.Positions)
            .FirstOrDefaultAsync(c => c.UserId == userId);
        if (connection is not null)
            return connection;

        connection = new WebullConnection { UserId = userId };
        _context.WebullConnections.Add(connection);
        return connection;
    }

    private static DateTime? AsUtc(DateTime? value) =>
        value is { } v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;

    /// <inheritdoc />
    public async Task SavePortfolioAsync(int userId, IReadOnlyList<WebullAccountPortfolio> accounts, DateTime fetchedAt, DateOnly easternDate)
    {
        // Throws on a duplicate account id before anything is loaded or written.
        var incoming = accounts.ToDictionary(a => a.Balance.AccountId);
        var connection = await GetOrCreateAsync(userId);
        var dailyRows = await _context.WebullDailyValues
            .Where(d => d.WebullConnectionId == connection.Id && d.EasternDate == easternDate)
            .ToDictionaryAsync(d => d.AccountId);

        // Dropped accounts take their tracked positions with them (cascade); their daily rows are history and stay.
        DropUnlisted(connection, incoming.Keys);
        foreach (var portfolio in incoming.Values)
        {
            var row = MergeAccount(connection, portfolio.Balance);
            row.TotalMarketValue = portfolio.TotalMarketValue;
            row.TotalDayProfitLoss = portfolio.TotalDayProfitLoss;
            row.TotalUnrealizedProfitLoss = portfolio.TotalUnrealizedProfitLoss;

            _context.WebullPositions.RemoveRange(row.Positions.ToList());
            foreach (var position in portfolio.Positions)
                row.Positions.Add(new WebullAccountPosition
                {
                    Symbol = position.Symbol,
                    InstrumentType = position.InstrumentType,
                    Quantity = position.Quantity,
                    LastPrice = position.LastPrice,
                    CostPrice = position.CostPrice,
                    UnrealizedProfitLoss = position.UnrealizedProfitLoss,
                    Currency = position.Currency
                });

            if (!dailyRows.TryGetValue(row.AccountId, out var daily))
            {
                // Added through the navigation so a brand-new connection's generated id flows into the key.
                daily = new WebullDailyValue { EasternDate = easternDate, AccountId = row.AccountId };
                connection.DailyValues.Add(daily);
            }
            daily.TotalValue = portfolio.Balance.TotalNetLiquidationValue;
            daily.Cash = portfolio.Balance.TotalCashBalance;
            daily.MarketValue = portfolio.TotalMarketValue;
            daily.DayProfitLoss = portfolio.TotalDayProfitLoss;
            daily.UnrealizedProfitLoss = portfolio.TotalUnrealizedProfitLoss;
            daily.Currency = portfolio.Balance.Currency;
            daily.FetchedAt = fetchedAt;
        }

        connection.LastFetchedAt = fetchedAt;
        connection.LastError = null;
        await SaveOrDiscardAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebullAccountPortfolio>> GetSavedPortfolioAsync(int userId)
    {
        var connection = await _context.WebullConnections
            .AsNoTracking()
            .Include(c => c.Accounts)
            .ThenInclude(a => a.Positions)
            .FirstOrDefaultAsync(c => c.UserId == userId);
        if (connection is null)
            return [];

        return connection.Accounts
            .OrderBy(a => a.Id)
            .Select(a => new WebullAccountPortfolio(
                ToBalance(a),
                a.TotalMarketValue,
                a.TotalDayProfitLoss,
                a.TotalUnrealizedProfitLoss,
                a.Positions
                    .OrderBy(p => p.Id)
                    .Select(p => new WebullPosition(
                        p.Symbol, p.InstrumentType, p.Quantity, p.LastPrice, p.CostPrice, p.UnrealizedProfitLoss, p.Currency))
                    .ToList()))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>> GetUserIdsWithTokenAsync() =>
        await _context.WebullConnections
            .AsNoTracking()
            .Where(c => c.AccessTokenEncrypted != null)
            .OrderBy(c => c.UserId)
            .Select(c => c.UserId)
            .ToListAsync();

    private void DropUnlisted(WebullConnection connection, IEnumerable<string> listedAccountIds)
    {
        var listed = listedAccountIds.ToHashSet();
        _context.WebullAccounts.RemoveRange(connection.Accounts.Where(a => !listed.Contains(a.AccountId)).ToList());
    }

    /// <summary>Finds or adds the connection's row for <paramref name="balance"/> and writes the v1 balance fields.</summary>
    private static WebullAccount MergeAccount(WebullConnection connection, WebullBalance balance)
    {
        var row = connection.Accounts.FirstOrDefault(a => a.AccountId == balance.AccountId);
        if (row is null)
        {
            row = new WebullAccount { AccountId = balance.AccountId };
            connection.Accounts.Add(row);
        }

        row.AccountLabel = balance.AccountLabel;
        row.AccountType = balance.AccountType;
        row.TotalNetLiquidationValue = balance.TotalNetLiquidationValue;
        row.TotalCashBalance = balance.TotalCashBalance;
        row.Currency = balance.Currency;
        return row;
    }

    private static WebullBalance ToBalance(WebullAccount a) =>
        new(a.AccountId, a.AccountLabel, a.AccountType, a.TotalNetLiquidationValue, a.TotalCashBalance, a.Currency);
}
