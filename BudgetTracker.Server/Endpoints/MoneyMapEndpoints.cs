using BudgetTracker.Server.Utilities;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;

namespace BudgetTracker.Server.Endpoints;

public static class MoneyMapEndpoints
{
    private const int DefaultMonths = 12;

    public static IEndpointRouteBuilder MapMoneyMapEndpoints(this IEndpointRouteBuilder moneyMapGroup)
    {
        // months: how many calendar months (ending with this one) the actuals average over.
        // today: the caller's local date (yyyy-MM-dd); used when within a day of UTC, else the UTC date.
        moneyMapGroup.MapGet("/", async (int? months, DateTime? today, IMoneyMapManager manager, ICurrentUserProvider currentUser) =>
        {
            var result = await manager.GetAsync(currentUser.UserId, months ?? DefaultMonths, WallClock.Unzoned(today));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
        });

        return moneyMapGroup;
    }
}
