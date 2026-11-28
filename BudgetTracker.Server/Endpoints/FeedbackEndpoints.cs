using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints;

/// <summary>Body of a feedback submission. <paramref name="Page"/> is the app path the user was on.</summary>
public record FeedbackRequest(string? Message, string? Page);

/// <summary><c>/api/feedback</c>: signed-in users send comments on the site; admins read them.</summary>
public static class FeedbackEndpoints
{
    /// <summary>Authorization policy for the admin read: the Cognito ID token's <c>cognito:groups</c> includes <see cref="AdminGroup"/>.</summary>
    public const string AdminPolicy = "Admin";

    /// <summary>The Cognito user pool group whose members may read feedback.</summary>
    public const string AdminGroup = "admin";

    /// <summary>The claim Cognito puts group membership in (inbound claim mapping is off).</summary>
    public const string GroupsClaim = "cognito:groups";

    public static IEndpointRouteBuilder MapFeedbackEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapPost("/", SubmitAsync);
        group.MapGet("/", GetPageAsync).RequireAuthorization(AdminPolicy);
        return group;
    }

    /// <summary><c>POST /</c>: 201 with <c>{ id }</c>; 400 <c>{ error }</c> when invalid; 429 <c>{ error }</c> when over the rate limit.</summary>
    public static async Task<IResult> SubmitAsync([FromBody] FeedbackRequest request, IFeedbackManager manager, ICurrentUserProvider currentUser)
    {
        var result = await manager.SubmitAsync(new Feedback
        {
            UserId = currentUser.UserId,
            Message = request.Message ?? string.Empty,
            Page = request.Page,
        });

        if (result.IsSuccess)
            return Results.Created($"/api/feedback/{result.Value}", new { id = result.Value });
        return result.Error == FeedbackErrors.RateLimited
            ? Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status429TooManyRequests)
            : Results.BadRequest(new { error = result.Error });
    }

    /// <summary><c>GET /?before=&amp;take=</c> (admin only): entries newest first; pass the last id seen as <c>before</c> for the next page.</summary>
    public static async Task<IResult> GetPageAsync(IFeedbackManager manager, [FromQuery] int? before = null, [FromQuery] int? take = null)
    {
        var result = await manager.GetPageAsync(before, take);
        return Results.Ok(result.Value);
    }
}
