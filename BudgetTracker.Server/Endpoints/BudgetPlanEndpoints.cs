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

        budgetPlanGroup.MapPost("/", CreateAsync);

        budgetPlanGroup.MapPut("/{id}", UpdateAsync);

        budgetPlanGroup.MapDelete("/{id}", async (int id, IBudgetPlanManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.DeleteAsync(id, currentUser.UserId);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        });

        return budgetPlanGroup;
    }

    /// <summary>
    /// <c>POST /api/budget-plans</c> — creates a plan for the current user. 201 on success; 400 with <c>{ error }</c>
    /// on a validation failure such as a duplicate name for the month.
    /// </summary>
    /// <param name="budgetPlan">The plan to create.</param>
    /// <param name="manager">Budget plan workflow.</param>
    /// <param name="currentUser">The authenticated user.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> CreateAsync(
        [FromBody] BudgetPlan budgetPlan,
        IBudgetPlanManager manager,
        ICurrentUserProvider currentUser)
    {
        var result = await manager.CreateAsync(budgetPlan, currentUser.UserId);
        return result.IsSuccess
            ? Results.Created($"/api/budget-plans/{result.Value}", budgetPlan)
            : Results.BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// <c>PUT /api/budget-plans/{id}</c> — updates one of the current user's plans. 200 on success; 404 when the plan
    /// does not exist; 400 with <c>{ error }</c> on a validation failure such as a duplicate name for the month.
    /// </summary>
    /// <param name="id">The route id; must match <paramref name="budgetPlan"/>.Id.</param>
    /// <param name="budgetPlan">The updated plan.</param>
    /// <param name="manager">Budget plan workflow.</param>
    /// <param name="currentUser">The authenticated user.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> UpdateAsync(
        int id,
        [FromBody] BudgetPlan budgetPlan,
        IBudgetPlanManager manager,
        ICurrentUserProvider currentUser)
    {
        if (id != budgetPlan.Id)
            return Results.BadRequest(new { error = "ID mismatch" });

        var result = await manager.UpdateAsync(budgetPlan, currentUser.UserId);
        if (result.IsSuccess)
            return Results.Ok(budgetPlan);

        return result.Error == "Budget plan not found"
            ? Results.NotFound(result.Error)
            : Results.BadRequest(new { error = result.Error });
    }
}