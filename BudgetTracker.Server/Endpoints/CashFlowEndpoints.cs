using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models.CashFlow;

namespace BudgetTracker.Server.Endpoints;

/// <summary>Money in and out of the user's accounts over time.</summary>
public static class CashFlowEndpoints
{
    /// <summary>Maps <c>GET /api/cash-flow</c> onto <paramref name="cashFlowGroup"/>.</summary>
    public static IEndpointRouteBuilder MapCashFlowEndpoints(this IEndpointRouteBuilder cashFlowGroup)
    {
        cashFlowGroup.MapGet("/", GetAsync);
        return cashFlowGroup;
    }

    /// <summary>
    /// <c>GET /api/cash-flow</c> — the cash-flow report. Dates are yyyy-MM-dd; <paramref name="to"/> and
    /// <paramref name="compareTo"/> include their whole day; no <paramref name="from"/> means All. 400 with
    /// <c>{ error }</c> for an unknown <paramref name="groupBy"/> or a From after To.
    /// </summary>
    /// <param name="from">First day of the window, or null for All.</param>
    /// <param name="to">Last day of the window.</param>
    /// <param name="groupBy">Day, Week, Month or Year, any case.</param>
    /// <param name="compareFrom">First day of the comparison window.</param>
    /// <param name="compareTo">Last day of the comparison window.</param>
    /// <param name="plaidAccountId">Only this linked account.</param>
    /// <param name="categoryId">Only this category.</param>
    /// <param name="search">Payee/notes substring.</param>
    /// <param name="manager">Cash-flow workflow.</param>
    /// <param name="currentUser">The authenticated user.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> GetAsync(
        DateTime? from,
        DateTime to,
        string? groupBy,
        DateTime? compareFrom,
        DateTime? compareTo,
        string? plaidAccountId,
        int? categoryId,
        string? search,
        ICashFlowManager manager,
        ICurrentUserProvider currentUser)
    {
        // Match on names: Enum.TryParse alone would also accept "7" or "Day,Week".
        if (groupBy is null || !Enum.GetNames<CashFlowGrouping>().Contains(groupBy, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "groupBy must be Day, Week, Month or Year" });

        var grouping = Enum.Parse<CashFlowGrouping>(groupBy, ignoreCase: true);
        var result = await manager.GetReportAsync(
            currentUser.UserId,
            new CashFlowQuery(from, to, grouping, compareFrom, compareTo, plaidAccountId, categoryId, search));

        return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
    }
}
