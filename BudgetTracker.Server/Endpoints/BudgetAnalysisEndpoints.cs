using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;

namespace BudgetTracker.Server.Endpoints;

public static class BudgetAnalysisEndpoints
{
    private const int DefaultTrendMonths = 3;

    public static IEndpointRouteBuilder MapBudgetAnalysisEndpoints(this IEndpointRouteBuilder budgetAnalysisGroup)
    {
        // from/to are date-only (yyyy-MM-dd); `to` is inclusive of its whole day.
        budgetAnalysisGroup.MapGet("/", async (
            DateTime from,
            DateTime to,
            IBudgetAnalysisManager manager,
            ICurrentUserProvider currentUser,
            int? trendMonths) =>
        {
            var result = await manager.GetAnalysisAsync(
                currentUser.UserId, from, to, trendMonths ?? DefaultTrendMonths);
            return Results.Ok(result.Value);
        });

        return budgetAnalysisGroup;
    }
}
