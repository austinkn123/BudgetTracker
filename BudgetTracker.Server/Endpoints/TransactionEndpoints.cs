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
        // Any filter query string switches to the filtered read; with none supplied the whole ledger
        // is returned, preserving the existing contract for callers that page through everything.
        transactionGroup.MapGet("/", async (
            ITransactionManager manager,
            ICurrentUserProvider currentUser,
            DateTime? from,
            DateTime? to,
            bool? uncategorized,
            bool? isImported,
            bool? isPending,
            string? search) =>
        {
            var hasFilter = from is not null || to is not null || uncategorized is not null
                || isImported is not null || isPending is not null
                || !string.IsNullOrWhiteSpace(search);

            var result = hasFilter
                ? await manager.GetFilteredAsync(
                    currentUser.UserId,
                    new TransactionFilter(from, to, uncategorized, isImported, isPending, search))
                : await manager.GetByUserIdAsync(currentUser.UserId);

            return Results.Ok(result.Value);
        });

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
}

/// <summary>Body for the bulk categorise endpoint. A null CategoryId clears the category.</summary>
public record BulkCategoryRequest(int[] Ids, int? CategoryId);

/// <summary>Body for the notes endpoint. A null or empty Notes clears the annotation.</summary>
public record NotesRequest(int Id, string? Notes);
