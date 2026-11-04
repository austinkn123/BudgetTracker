using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Webull;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

/// <summary>
/// Persists the user's Webull order history and each account's sync progress. Insert-or-overwrite only, never deletes.
/// Columns are <c>timestamp without time zone</c> holding UTC, so every read restores the UTC kind.
/// </summary>
public class WebullOrderAccessor : IWebullOrderAccessor
{
    private readonly BudgetTrackerDbContext _context;

    /// <summary>Initializes the accessor.</summary>
    /// <param name="context">The scoped DbContext.</param>
    public WebullOrderAccessor(BudgetTrackerDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebullOrderSync>> GetSyncsAsync(int userId)
    {
        var rows = await _context.WebullOrderSyncs
            .AsNoTracking()
            .Where(s => _context.WebullConnections.Any(c => c.Id == s.WebullConnectionId && c.UserId == userId))
            .OrderBy(s => s.AccountId)
            .ToListAsync();

        return rows.Select(s => new WebullOrderSync(
                s.AccountId,
                AsUtc(s.CoveredFrom),
                AsUtc(s.CoveredTo),
                s is { WindowStart: { } start, WindowEnd: { } end } ? new WebullOrderWindow(AsUtc(start), AsUtc(end), s.PaginationKey) : null,
                s.LastError))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WebullOrder>> GetOrdersAsync(int userId)
    {
        var rows = await _context.WebullOrders
            .AsNoTracking()
            .Where(o => _context.WebullConnections.Any(c => c.Id == o.WebullConnectionId && c.UserId == userId))
            .OrderBy(o => o.AccountId)
            .ThenBy(o => o.OrderId)
            .ToListAsync();

        return rows.Select(o => new WebullOrder(
                o.AccountId, o.OrderId, o.Symbol, o.InstrumentType, o.Side, o.Status, o.FilledQuantity, o.FilledPrice,
                AsUtc(o.PlacedAt), AsUtc(o.FilledAt)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task SavePageAsync(int userId, WebullOrderSync sync, IReadOnlyList<WebullOrder> orders)
    {
        if (orders.Any(o => o.AccountId != sync.AccountId))
            throw new ArgumentException("Every order must belong to the sync's account.", nameof(orders));

        var connectionId = await _context.WebullConnections
            .Where(c => c.UserId == userId)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("The user has no Webull connection.");

        // Ordinal, like the key; the last duplicate wins.
        var incoming = new Dictionary<string, WebullOrder>(StringComparer.Ordinal);
        foreach (var order in orders)
            incoming[order.OrderId] = order;

        var ids = incoming.Keys.ToList();
        var existing = await _context.WebullOrders
            .Where(o => o.WebullConnectionId == connectionId && o.AccountId == sync.AccountId && ids.Contains(o.OrderId))
            .ToDictionaryAsync(o => o.OrderId, StringComparer.Ordinal);

        foreach (var order in incoming.Values)
        {
            if (!existing.TryGetValue(order.OrderId, out var row))
            {
                row = new WebullAccountOrder { WebullConnectionId = connectionId, AccountId = order.AccountId, OrderId = order.OrderId };
                _context.WebullOrders.Add(row);
            }

            row.Symbol = order.Symbol;
            row.InstrumentType = order.InstrumentType;
            row.Side = order.Side;
            row.Status = order.Status;
            row.FilledQuantity = order.FilledQuantity;
            row.FilledPrice = order.FilledPrice;
            row.PlacedAt = order.PlacedAt;
            row.FilledAt = order.FilledAt;
        }

        var syncRow = await _context.WebullOrderSyncs.FindAsync(connectionId, sync.AccountId);
        if (syncRow is null)
        {
            syncRow = new WebullAccountOrderSync { WebullConnectionId = connectionId, AccountId = sync.AccountId };
            _context.WebullOrderSyncs.Add(syncRow);
        }

        syncRow.CoveredFrom = sync.CoveredFrom;
        syncRow.CoveredTo = sync.CoveredTo;
        syncRow.WindowStart = sync.Window?.Start;
        syncRow.WindowEnd = sync.Window?.End;
        syncRow.PaginationKey = sync.Window?.PaginationKey;
        syncRow.LastError = sync.LastError;

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

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(DateTime? value) => value is { } v ? AsUtc(v) : null;
}
