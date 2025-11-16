using BudgetTracker.Application.Interfaces;
using BudgetTracker.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints
{
    public static class CategoryEndpoints
    {
        public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder categoryGroup)
        {
            categoryGroup.MapGet("/{id}", async (int id, ICategoryRepository repo) =>
            {
                var category = await repo.GetByIdAsync(id);
                return category is not null ? Results.Ok(category) : Results.NotFound();
            });

            categoryGroup.MapGet("/user/{userId}", async (int userId, ICategoryRepository repo) =>
            {
                var categories = await repo.GetByUserIdAsync(userId);
                return Results.Ok(categories);
            });

            categoryGroup.MapPost("/", async ([FromBody] Category category, ICategoryRepository repo) =>
            {
                var id = await repo.CreateAsync(category);
                return Results.Created($"/api/categories/{id}", category);
            });

            categoryGroup.MapPut("/{id}", async (int id, [FromBody] Category category, ICategoryRepository repo) =>
            {
                if (id != category.Id)
                {
                    return Results.BadRequest("ID mismatch");
                }
                var updated = await repo.UpdateAsync(category);
                return updated ? Results.Ok(category) : Results.NotFound();
            });

            categoryGroup.MapDelete("/{id}", async (int id, ICategoryRepository repo) =>
            {
                var deleted = await repo.DeleteAsync(id);
                return deleted ? Results.NoContent() : Results.NotFound();
            });

            return categoryGroup;
        }
    }
}
