using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder userGroup)
    {
        userGroup.MapGet("/me", async (ICurrentUserProvider currentUser, IUserManager manager) =>
        {
            var result = await manager.GetByIdAsync(currentUser.UserId);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
        });

        userGroup.MapPut("/me", async ([FromBody] User user, IUserManager manager, ICurrentUserProvider currentUser) =>
        {
            user.Id = currentUser.UserId;
            var result = await manager.UpdateAsync(user);
            return result.IsSuccess ? Results.Ok(user) : Results.NotFound();
        });

        return userGroup;
    }
}
