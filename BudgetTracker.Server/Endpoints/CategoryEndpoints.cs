using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder categoryGroup)
    {
        categoryGroup.MapGet("/", async (ICategoryManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.GetByUserIdAsync(currentUser.UserId);
            return Results.Ok(result.Value);
        });

        categoryGroup.MapGet("/{id}", async (int id, ICategoryManager manager) =>
        {
            var result = await manager.GetByIdAsync(id);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });

        categoryGroup.MapPost("/", async ([FromBody] Category category, ICategoryManager manager, ICurrentUserProvider currentUser) =>
        {
            category.UserId = currentUser.UserId;
            var result = await manager.CreateAsync(category);
            return result.IsSuccess
                ? Results.Created($"/api/categories/{result.Value}", category)
                : Results.BadRequest(result.Error);
        });

        categoryGroup.MapPut("/{id}", async (int id, [FromBody] Category category, ICategoryManager manager, ICurrentUserProvider currentUser) =>
        {
            if (id != category.Id)
                return Results.BadRequest("ID mismatch");

            category.UserId = currentUser.UserId;
            var result = await manager.UpdateAsync(category);
            return result.IsSuccess ? Results.Ok(category) : Results.NotFound();
        });

        categoryGroup.MapDelete("/{id}", async (int id, ICategoryManager manager) =>
        {
            var result = await manager.DeleteAsync(id);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound();
        });

        return categoryGroup;
    }
}
