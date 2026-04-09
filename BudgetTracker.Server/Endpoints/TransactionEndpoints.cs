using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints;

public static class TransactionEndpoints
{
    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder transactionGroup)
    {
        transactionGroup.MapGet("/", async (ITransactionManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.GetByUserIdAsync(currentUser.UserId);
            return Results.Ok(result.Value);
        });

        transactionGroup.MapGet("/{id}", async (int id, ITransactionManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.GetByIdAsync(id, currentUser.UserId);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });

        transactionGroup.MapPost("/", async ([FromBody] Transaction transaction, ITransactionManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.CreateAsync(transaction, currentUser.UserId);
            return result.IsSuccess
                ? Results.Created($"/api/transactions/{result.Value}", transaction)
                : Results.BadRequest(result.Error);
        });

        transactionGroup.MapPut("/{id}", async (int id, [FromBody] Transaction transaction, ITransactionManager manager, ICurrentUserProvider currentUser) =>
        {
            if (id != transaction.Id)
                return Results.BadRequest("ID mismatch");

            var result = await manager.UpdateAsync(transaction, currentUser.UserId);
            return result.IsSuccess ? Results.Ok(transaction) : Results.NotFound();
        });

        transactionGroup.MapDelete("/{id}", async (int id, ITransactionManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.DeleteAsync(id, currentUser.UserId);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound();
        });

        return transactionGroup;
    }
}
