using BudgetTracker.Application.Interfaces;
using BudgetTracker.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints
{
    public static class ExpenseEndpoints
    {
        public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder expenseGroup)
        {
            expenseGroup.MapGet("/{id}", async (int id, IExpenseRepository repo) =>
            {
                var expense = await repo.GetByIdAsync(id);
                return expense is not null ? Results.Ok(expense) : Results.NotFound();
            });

            expenseGroup.MapGet("/user/{userId}", async (int userId, IExpenseRepository repo) =>
            {
                var expenses = await repo.GetByUserIdAsync(userId);
                return Results.Ok(expenses);
            });

            expenseGroup.MapPost("/", async ([FromBody] Expense expense, IExpenseRepository repo) =>
            {
                var id = await repo.CreateAsync(expense);
                return Results.Created($"/api/expenses/{id}", expense);
            });

            expenseGroup.MapPut("/{id}", async (int id, [FromBody] Expense expense, IExpenseRepository repo) =>
            {
                if (id != expense.Id)
                {
                    return Results.BadRequest("ID mismatch");
                }
                var updated = await repo.UpdateAsync(expense);
                return updated ? Results.Ok(expense) : Results.NotFound();
            });

            expenseGroup.MapDelete("/{id}", async (int id, IExpenseRepository repo) =>
            {
                var deleted = await repo.DeleteAsync(id);
                return deleted ? Results.NoContent() : Results.NotFound();
            });

            return expenseGroup;
        }
    }
}
