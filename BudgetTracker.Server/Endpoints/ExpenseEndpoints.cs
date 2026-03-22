using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints;

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder expenseGroup)
    {
        expenseGroup.MapGet("/", async (IExpenseManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.GetByUserIdAsync(currentUser.UserId);
            return Results.Ok(result.Value);
        });

        expenseGroup.MapGet("/{id}", async (int id, IExpenseManager manager) =>
        {
            var result = await manager.GetByIdAsync(id);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });

        expenseGroup.MapPost("/", async ([FromBody] Expense expense, IExpenseManager manager, ICurrentUserProvider currentUser) =>
        {
            expense.UserId = currentUser.UserId;
            var result = await manager.CreateAsync(expense);
            return result.IsSuccess
                ? Results.Created($"/api/expenses/{result.Value}", expense)
                : Results.BadRequest(result.Error);
        });

        expenseGroup.MapPut("/{id}", async (int id, [FromBody] Expense expense, IExpenseManager manager, ICurrentUserProvider currentUser) =>
        {
            if (id != expense.Id)
                return Results.BadRequest("ID mismatch");

            expense.UserId = currentUser.UserId;
            var result = await manager.UpdateAsync(expense);
            return result.IsSuccess ? Results.Ok(expense) : Results.NotFound();
        });

        expenseGroup.MapDelete("/{id}", async (int id, IExpenseManager manager) =>
        {
            var result = await manager.DeleteAsync(id);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound();
        });

        return expenseGroup;
    }
}
