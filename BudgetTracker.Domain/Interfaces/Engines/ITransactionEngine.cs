using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>Pure ledger-read rules: when a filtered read is allowed, page sizes, and the opaque page cursor. No I/O.</summary>
public interface ITransactionEngine
{
    /// <summary>Whether <paramref name="filter"/> narrows the ledger at all; a blank search counts as no filter.</summary>
    /// <param name="filter">The requested filter.</param>
    /// <returns>True when at least one constraint is set.</returns>
    bool HasAnyFilter(TransactionFilter filter);

    /// <summary>The page size to use: 50 when none is requested, otherwise clamped to 1–200.</summary>
    /// <param name="limit">The requested page size, if any.</param>
    /// <returns>The effective page size.</returns>
    int ResolvePageSize(int? limit);

    /// <summary>Decode an opaque cursor produced by <see cref="EncodeCursor"/>.</summary>
    /// <param name="cursor">The cursor from the request, or null/blank for the first page.</param>
    /// <returns>The decoded position (null for the first page), or a failure when the cursor is malformed.</returns>
    Result<TransactionCursor?> ParseCursor(string? cursor);

    /// <summary>Encode a keyset position as an opaque base64url string.</summary>
    /// <param name="cursor">The position.</param>
    /// <returns>The opaque cursor.</returns>
    string EncodeCursor(TransactionCursor cursor);

    /// <summary>
    /// Turn a read of up to <paramref name="pageSize"/> + 1 rows into a page: the extra row, when present, only signals
    /// that another page exists, and the cursor points at the last row kept.
    /// </summary>
    /// <param name="fetched">Rows in page order, at most <paramref name="pageSize"/> + 1 of them.</param>
    /// <param name="pageSize">The effective page size.</param>
    /// <returns>The page.</returns>
    TransactionPage BuildPage(IReadOnlyList<Transaction> fetched, int pageSize);
}
