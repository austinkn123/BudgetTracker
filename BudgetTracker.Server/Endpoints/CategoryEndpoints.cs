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

        categoryGroup.MapGet("/{id}", async (int id, ICategoryManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.GetByIdAsync(id, currentUser.UserId);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });

        categoryGroup.MapPost("/", CreateAsync);

        categoryGroup.MapPut("/{id}", UpdateAsync);

        categoryGroup.MapDelete("/{id}", async (int id, ICategoryManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.DeleteAsync(id, currentUser.UserId);
            if (result.IsSuccess)
                return Results.NoContent();

            return result.Error == "Category not found"
                ? Results.NotFound()
                : Results.BadRequest(result.Error);
        });

        return categoryGroup;
    }

    /// <summary>
    /// <c>POST /api/categories</c> — creates a category for the current user. 201 on success; 400 with <c>{ error }</c>
    /// on a validation failure such as a duplicate name.
    /// </summary>
    /// <param name="category">The category to create.</param>
    /// <param name="manager">Category workflow.</param>
    /// <param name="currentUser">The authenticated user.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> CreateAsync(
        [FromBody] Category category,
        ICategoryManager manager,
        ICurrentUserProvider currentUser)
    {
        category.UserId = currentUser.UserId;
        var result = await manager.CreateAsync(category);
        return result.IsSuccess
            ? Results.Created($"/api/categories/{result.Value}", category)
            : Results.BadRequest(new { error = result.Error });
    }

    /// <summary>
    /// <c>PUT /api/categories/{id}</c> — updates one of the current user's categories. 200 on success; 404 when the
    /// category does not exist; 400 with <c>{ error }</c> on a validation failure such as a duplicate name.
    /// </summary>
    /// <param name="id">The route id; must match <paramref name="category"/>.Id.</param>
    /// <param name="category">The updated category.</param>
    /// <param name="manager">Category workflow.</param>
    /// <param name="currentUser">The authenticated user.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> UpdateAsync(
        int id,
        [FromBody] Category category,
        ICategoryManager manager,
        ICurrentUserProvider currentUser)
    {
        if (id != category.Id)
            return Results.BadRequest(new { error = "ID mismatch" });

        category.UserId = currentUser.UserId;
        var result = await manager.UpdateAsync(category);
        if (result.IsSuccess)
            return Results.Ok(category);

        return result.Error == "Category not found"
            ? Results.NotFound()
            : Results.BadRequest(new { error = result.Error });
    }
}
