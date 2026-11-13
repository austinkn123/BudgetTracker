using System.Buffers.Binary;
using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Engines;

/// <inheritdoc />
public class TransactionEngine : ITransactionEngine
{
    private const int DefaultPageSize = 50;
    private const int MinPageSize = 1;
    private const int MaxPageSize = 200;
    private const string InvalidCursor = "Invalid cursor";

    // Cursor payload: OccurredAt ticks (8 bytes) then Id (4 bytes), little-endian, base64url without padding.
    private const int CursorLength = sizeof(long) + sizeof(int);

    /// <inheritdoc />
    public bool HasAnyFilter(TransactionFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return filter.From is not null || filter.To is not null || filter.Uncategorized is not null
            || filter.IsImported is not null || filter.IsPending is not null
            || !string.IsNullOrWhiteSpace(filter.Search)
            || !string.IsNullOrWhiteSpace(filter.PlaidAccountId) || filter.CategoryId is not null;
    }

    /// <inheritdoc />
    public int ResolvePageSize(int? limit) => Math.Clamp(limit ?? DefaultPageSize, MinPageSize, MaxPageSize);

    /// <inheritdoc />
    public Result<TransactionCursor?> ParseCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return Result<TransactionCursor?>.Success(null);

        Span<byte> bytes = stackalloc byte[CursorLength + 3];
        if (!TryDecodeBase64Url(cursor, bytes, out var written) || written != CursorLength)
            return Result<TransactionCursor?>.Failure(InvalidCursor);

        var ticks = BinaryPrimitives.ReadInt64LittleEndian(bytes);
        var id = BinaryPrimitives.ReadInt32LittleEndian(bytes[sizeof(long)..]);
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks || id <= 0)
            return Result<TransactionCursor?>.Failure(InvalidCursor);

        return Result<TransactionCursor?>.Success(new TransactionCursor(new DateTime(ticks, DateTimeKind.Unspecified), id));
    }

    /// <inheritdoc />
    public string EncodeCursor(TransactionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        Span<byte> bytes = stackalloc byte[CursorLength];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, cursor.OccurredAt.Ticks);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[sizeof(long)..], cursor.Id);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <inheritdoc />
    public TransactionPage BuildPage(IReadOnlyList<Transaction> fetched, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(fetched);

        if (fetched.Count <= pageSize)
            return new TransactionPage(fetched, null);

        var items = fetched.Take(pageSize).ToList();
        var last = items[^1];
        return new TransactionPage(items, EncodeCursor(new TransactionCursor(last.OccurredAt, last.Id)));
    }

    private static bool TryDecodeBase64Url(string value, Span<byte> destination, out int written)
    {
        written = 0;
        if (value.Length > 64)
            return false;

        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            2 => base64 + "==",
            3 => base64 + "=",
            0 => base64,
            _ => null
        } ?? string.Empty;

        return base64.Length > 0 && Convert.TryFromBase64String(base64, destination, out written);
    }
}
