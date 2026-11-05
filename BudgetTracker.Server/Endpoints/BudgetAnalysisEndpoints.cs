using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;

namespace BudgetTracker.Server.Endpoints;

public static class BudgetAnalysisEndpoints
{
    private const int DefaultTrendMonths = 3;

    public static IEndpointRouteBuilder MapBudgetAnalysisEndpoints(this IEndpointRouteBuilder budgetAnalysisGroup)
    {
        budgetAnalysisGroup.MapGet("/", GetAsync);

        return budgetAnalysisGroup;
    }

    /// <summary>
    /// <c>GET /api/budget-analysis</c> — plan-vs-actual for the month the window ends in, window spend, and the per-category
    /// trend. 400 with <c>{ error }</c> when <paramref name="trendMonths"/> is outside 1–24.
    /// </summary>
    /// <param name="from">First day of the window (yyyy-MM-dd).</param>
    /// <param name="to">Last day of the window (yyyy-MM-dd), inclusive of its whole day.</param>
    /// <param name="manager">Budget analysis workflow.</param>
    /// <param name="currentUser">The authenticated user.</param>
    /// <param name="trendMonths">Trend length in months; 3 by default.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> GetAsync(
        DateTime from,
        DateTime to,
        IBudgetAnalysisManager manager,
        ICurrentUserProvider currentUser,
        int? trendMonths)
    {
        var result = await manager.GetAnalysisAsync(currentUser.UserId, from, to, trendMonths ?? DefaultTrendMonths);
        return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
    }
}
