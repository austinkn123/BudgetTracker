using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints;

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder expenseGroup)
    {
        expenseGroup.MapGet("/{id}", async (int id, IExpenseManager manager) =>
        {
            var result = await manager.GetByIdAsync(id);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });

        expenseGroup.MapGet("/user/{userId}", async (int userId, IExpenseManager manager) =>
        {
            var result = await manager.GetByUserIdAsync(userId);
            return Results.Ok(result.Value);
        });

        expenseGroup.MapPost("/", async ([FromBody] Expense expense, IExpenseManager manager) =>
        {
            var result = await manager.CreateAsync(expense);
            return result.IsSuccess
                ? Results.Created($"/api/expenses/{result.Value}", expense)
                : Results.BadRequest(result.Error);
        });

        expenseGroup.MapPut("/{id}", async (int id, [FromBody] Expense expense, IExpenseManager manager) =>
        {
            if (id != expense.Id)
                return Results.BadRequest("ID mismatch");

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
