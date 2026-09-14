using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;

namespace BudgetTracker.Server.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder userGroup)
    {
        userGroup.MapGet("/me", async (ICurrentUserProvider currentUser, IUserManager manager) =>
        {
            var result = await manager.GetByIdAsync(currentUser.UserId);
            if (!result.IsSuccess)
                return Results.NotFound();

            var user = result.Value!;
            return Results.Ok(new UserProfile(user.Id, currentUser.Email, user.CreatedAt));
        });

        return userGroup;
    }
}
