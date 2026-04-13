using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints;

public static class BudgetPlanEndpoints
{
    public static IEndpointRouteBuilder MapBudgetPlanEndpoints(this IEndpointRouteBuilder budgetPlanGroup)
    {
        budgetPlanGroup.MapGet("/", async (IBudgetPlanManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.GetByUserIdAsync(currentUser.UserId);
            return Results.Ok(result.Value);
        });

        budgetPlanGroup.MapGet("/{id}", async (int id, IBudgetPlanManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.GetByIdAsync(id, currentUser.UserId);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });

        budgetPlanGroup.MapPost("/", async ([FromBody] BudgetPlan budgetPlan, IBudgetPlanManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.CreateAsync(budgetPlan, currentUser.UserId);
            return result.IsSuccess
                ? Results.Created($"/api/budget-plans/{result.Value}", budgetPlan)
                : Results.BadRequest(result.Error);
        });

        budgetPlanGroup.MapPut("/{id}", async (int id, [FromBody] BudgetPlan budgetPlan, IBudgetPlanManager manager, ICurrentUserProvider currentUser) =>
        {
            if (id != budgetPlan.Id)
                return Results.BadRequest("ID mismatch");

            var result = await manager.UpdateAsync(budgetPlan, currentUser.UserId);
            return result.IsSuccess ? Results.Ok(budgetPlan) : Results.NotFound(result.Error);
        });

        budgetPlanGroup.MapDelete("/{id}", async (int id, IBudgetPlanManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.DeleteAsync(id, currentUser.UserId);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        });

        return budgetPlanGroup;
    }
}