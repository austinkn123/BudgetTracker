using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models.Analysis;

namespace BudgetTracker.Domain.Interfaces.Managers;

public interface IBudgetAnalysisManager
{
    /// <summary>
    /// Analyse the user's active plan, plus spend across [<paramref name="from"/>, <paramref name="to"/>]
    /// (inclusive of the whole end day) and a <paramref name="trendMonths"/>-month per-category trend.
    /// </summary>
    Task<Result<BudgetAnalysis>> GetAnalysisAsync(int userId, DateTime from, DateTime to, int trendMonths);
}
