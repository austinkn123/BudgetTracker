using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder userGroup)
    {
        userGroup.MapGet("/{id}", async (int id, IUserManager manager) =>
        {
            var result = await manager.GetByIdAsync(id);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });

        userGroup.MapGet("/cognito/{cognitoId}", async (string cognitoId, IUserManager manager) =>
        {
            var result = await manager.GetByCognitoIdAsync(cognitoId);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });

        userGroup.MapPost("/", async ([FromBody] User user, IUserManager manager) =>
        {
            var result = await manager.CreateAsync(user);
            return result.IsSuccess
                ? Results.Created($"/api/users/{result.Value}", user)
                : Results.BadRequest(result.Error);
        });

        userGroup.MapPut("/{id}", async (int id, [FromBody] User user, IUserManager manager) =>
        {
            if (id != user.Id)
                return Results.BadRequest("ID mismatch");

            var result = await manager.UpdateAsync(user);
            return result.IsSuccess ? Results.Ok(user) : Results.NotFound();
        });

        userGroup.MapDelete("/{id}", async (int id, IUserManager manager) =>
        {
            var result = await manager.DeleteAsync(id);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound();
        });

        return userGroup;
    }
}
