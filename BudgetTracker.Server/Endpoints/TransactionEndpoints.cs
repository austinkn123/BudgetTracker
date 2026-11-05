using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints;

/// <summary>
/// Read and review endpoints for the ledger. Plaid sync is the only creator of transactions, so
/// there is deliberately no POST or DELETE here — the two writes a person can make are assigning a
/// category and annotating a row, both of which apply to many rows at once.
/// </summary>
public static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder transactionGroup)
    {
        transactionGroup.MapGet("/", GetAsync);
        transactionGroup.MapGet("/page", GetPageAsync);
        transactionGroup.MapGet("/summary", GetSummaryAsync);

        transactionGroup.MapPatch("/category", async (
            [FromBody] BulkCategoryRequest request,
            ITransactionManager manager,
            ICurrentUserProvider currentUser) =>
        {
            var result = await manager.SetCategoryAsync(request.Ids, request.CategoryId, currentUser.UserId);
            return result.IsSuccess
                ? Results.Ok(new { updated = result.Value })
                : Results.BadRequest(result.Error);
        });

        transactionGroup.MapPatch("/notes", async (
            [FromBody] NotesRequest request,
            ITransactionManager manager,
            ICurrentUserProvider currentUser) =>
        {
            var result = await manager.SetNotesAsync(request.Id, request.Notes, currentUser.UserId);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        });

        return transactionGroup;
    }

    /// <summary>
    /// <c>GET /api/transactions</c> — the ledger narrowed by at least one filter, as an array, newest first. 400 with
    /// <c>{ error }</c> when no filter is given: the whole ledger is only served a page at a time by
    /// <c>GET /api/transactions/page</c>.
    /// </summary>
    /// <param name="manager">Ledger workflow.</param>
    /// <param name="currentUser">The authenticated user.</param>
    /// <param name="from">First day (yyyy-MM-dd), inclusive.</param>
    /// <param name="to">Last day (yyyy-MM-dd), inclusive of its whole day.</param>
    /// <param name="uncategorized">True: only rows with no category; false: only categorised rows.</param>
    /// <param name="isImported">True: only Plaid-imported rows; false: only manual rows.</param>
    /// <param name="isPending">Only pending (true) or posted (false) rows.</param>
    /// <param name="search">Case-insensitive payee/notes substring.</param>
    /// <param name="plaidAccountId">Only rows from this Plaid account.</param>
    /// <param name="categoryId">Only rows in this category.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> GetAsync(
        ITransactionManager manager,
        ICurrentUserProvider currentUser,
        DateTime? from,
        DateTime? to,
        bool? uncategorized,
        bool? isImported,
        bool? isPending,
        string? search,
        string? plaidAccountId,
        int? categoryId)
    {
        var result = await manager.GetFilteredAsync(
            currentUser.UserId,
            new TransactionFilter(from, to, uncategorized, isImported, isPending, search, plaidAccountId, categoryId));

        return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// <c>GET /api/transactions/page</c> — one keyset page of the ledger, newest first (OccurredAt DESC, Id DESC), as
    /// <c>{ items, nextCursor }</c>. Takes the same optional filters as <see cref="GetAsync"/>. 400 with <c>{ error }</c>
    /// for a cursor this API did not issue.
    /// </summary>
    /// <param name="manager">Ledger workflow.</param>
    /// <param name="currentUser">The authenticated user.</param>
    /// <param name="from">First day (yyyy-MM-dd), inclusive.</param>
    /// <param name="to">Last day (yyyy-MM-dd), inclusive of its whole day.</param>
    /// <param name="uncategorized">True: only rows with no category; false: only categorised rows.</param>
    /// <param name="isImported">True: only Plaid-imported rows; false: only manual rows.</param>
    /// <param name="isPending">Only pending (true) or posted (false) rows.</param>
    /// <param name="search">Case-insensitive payee/notes substring.</param>
    /// <param name="plaidAccountId">Only rows from this Plaid account.</param>
    /// <param name="categoryId">Only rows in this category.</param>
    /// <param name="cursor">The previous page's <c>nextCursor</c>; omit for the first page.</param>
    /// <param name="limit">Page size; 50 by default, clamped to 1–200.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> GetPageAsync(
        ITransactionManager manager,
        ICurrentUserProvider currentUser,
        DateTime? from,
        DateTime? to,
        bool? uncategorized,
        bool? isImported,
        bool? isPending,
        string? search,
        string? plaidAccountId,
        int? categoryId,
        string? cursor,
        int? limit)
    {
        var result = await manager.GetPageAsync(
            currentUser.UserId,
            new TransactionFilter(from, to, uncategorized, isImported, isPending, search, plaidAccountId, categoryId),
            cursor,
            limit);

        return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// <c>GET /api/transactions/summary</c> — <c>{ uncategorizedCount, pendingByAccount: [{ plaidAccountId, amount }] }</c>,
    /// aggregated in the database.
    /// </summary>
    /// <param name="manager">Ledger workflow.</param>
    /// <param name="currentUser">The authenticated user.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> GetSummaryAsync(ITransactionManager manager, ICurrentUserProvider currentUser)
    {
        var result = await manager.GetSummaryAsync(currentUser.UserId);
        return Results.Ok(result.Value);
    }
}

/// <summary>Body for the bulk categorise endpoint. A null CategoryId clears the category.</summary>
public record BulkCategoryRequest(int[] Ids, int? CategoryId);

/// <summary>Body for the notes endpoint. A null or empty Notes clears the annotation.</summary>
public record NotesRequest(int Id, string? Notes);
