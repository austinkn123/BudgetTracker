using BudgetTracker.Application.Interfaces;
using BudgetTracker.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints
{
    public static class UserEndpoints
    {
        public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder userGroup)
        {
            userGroup.MapGet("/{id}", async (int id, IUserRepository repo) =>
            {
                var user = await repo.GetByIdAsync(id);
                return user is not null ? Results.Ok(user) : Results.NotFound();
            });

            userGroup.MapGet("/cognito/{cognitoId}", async (string cognitoId, IUserRepository repo) =>
            {
                var user = await repo.GetByCognitoIdAsync(cognitoId);
                return user is not null ? Results.Ok(user) : Results.NotFound();
            });

            userGroup.MapPost("/", async ([FromBody] User user, IUserRepository repo) =>
            {
                var id = await repo.CreateAsync(user);
                return Results.Created($"/api/users/{id}", user);
            });

            userGroup.MapPut("/{id}", async (int id, [FromBody] User user, IUserRepository repo) =>
            {
                if (id != user.Id)
                {
                    return Results.BadRequest("ID mismatch");
                }
                var updated = await repo.UpdateAsync(user);
                return updated ? Results.Ok(user) : Results.NotFound();
            });

            userGroup.MapDelete("/{id}", async (int id, IUserRepository repo) =>
            {
                var deleted = await repo.DeleteAsync(id);
                return deleted ? Results.NoContent() : Results.NotFound();
            });

            return userGroup;
        }
    }
}
