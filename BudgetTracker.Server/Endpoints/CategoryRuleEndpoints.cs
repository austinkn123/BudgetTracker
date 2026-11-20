using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Server.Endpoints;

/// <summary>Body of a rule create, update or preview. <paramref name="ApplyToExisting"/> is read by create only.</summary>
public record CategoryRuleRequest(string? Pattern, string? MatchType, int CategoryId, bool ApplyToExisting = false);

/// <summary><c>/api/category-rules</c>: "always categorize this payee as" rules (BUD-24).</summary>
public static class CategoryRuleEndpoints
{
    private const string NotFound = "Rule not found";

    public static IEndpointRouteBuilder MapCategoryRuleEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/", GetAllAsync);
        group.MapPost("/preview", PreviewAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id}", UpdateAsync);
        group.MapDelete("/{id}", DeleteAsync);
        return group;
    }

    /// <summary><c>GET /</c> — the user's rules, oldest first, each with its matched count.</summary>
    public static async Task<IResult> GetAllAsync(ICategoryRuleManager manager, ICurrentUserProvider currentUser)
    {
        var result = await manager.GetAllAsync(currentUser.UserId);
        return Results.Ok(result.Value);
    }

    /// <summary><c>POST /preview</c> — <c>{ count }</c> of uncategorized rows the rule would file; 400 <c>{ error }</c> when invalid.</summary>
    public static async Task<IResult> PreviewAsync([FromBody] CategoryRuleRequest request, ICategoryRuleManager manager, ICurrentUserProvider currentUser)
    {
        var result = await manager.PreviewAsync(ToRule(request, currentUser.UserId));
        return result.IsSuccess ? Results.Ok(new { count = result.Value }) : Results.BadRequest(new { error = result.Error });
    }

    /// <summary><c>POST /</c> — 201 with <c>{ id, appliedCount }</c>; 400 <c>{ error }</c> on a validation failure or duplicate.</summary>
    public static async Task<IResult> CreateAsync([FromBody] CategoryRuleRequest request, ICategoryRuleManager manager, ICurrentUserProvider currentUser)
    {
        var result = await manager.CreateAsync(ToRule(request, currentUser.UserId), request.ApplyToExisting);
        return result.IsSuccess
            ? Results.Created($"/api/category-rules/{result.Value!.Id}", result.Value)
            : Results.BadRequest(new { error = result.Error });
    }

    /// <summary><c>PUT /{id}</c> — 204; 404 when the rule isn't the user's; 400 <c>{ error }</c> otherwise.</summary>
    public static async Task<IResult> UpdateAsync(int id, [FromBody] CategoryRuleRequest request, ICategoryRuleManager manager, ICurrentUserProvider currentUser)
    {
        var rule = ToRule(request, currentUser.UserId);
        rule.Id = id;
        var result = await manager.UpdateAsync(rule);
        if (result.IsSuccess)
            return Results.NoContent();
        return result.Error == NotFound ? Results.NotFound() : Results.BadRequest(new { error = result.Error });
    }

    /// <summary><c>DELETE /{id}</c> — 204; 404 when the rule isn't the user's. Rows it filed keep their category.</summary>
    public static async Task<IResult> DeleteAsync(int id, ICategoryRuleManager manager, ICurrentUserProvider currentUser)
    {
        var result = await manager.DeleteAsync(id, currentUser.UserId);
        return result.IsSuccess ? Results.NoContent() : Results.NotFound();
    }

    private static CategoryRule ToRule(CategoryRuleRequest request, int userId) => new()
    {
        UserId = userId,
        Pattern = request.Pattern ?? string.Empty,
        MatchType = request.MatchType ?? string.Empty,
        CategoryId = request.CategoryId,
    };
}
