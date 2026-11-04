using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models.CashFlow;

namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>Orchestrates the cash-flow report: fetch the user's rows and balances, hand them to the engine.</summary>
public interface ICashFlowManager
{
    /// <summary>The cash-flow report for <paramref name="query"/>; a failure when the window is invalid.</summary>
    Task<Result<CashFlowReport>> GetReportAsync(int userId, CashFlowQuery query);
}
