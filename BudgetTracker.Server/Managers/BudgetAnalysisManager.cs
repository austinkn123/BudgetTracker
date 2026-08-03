using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models.Analysis;

namespace BudgetTracker.Server.Managers;

public class BudgetAnalysisManager(
    IBudgetAnalysisEngine engine,
    ITransactionAccessor transactionAccessor,
    IBudgetPlanAccessor budgetPlanAccessor) : IBudgetAnalysisManager
{
    public async Task<Result<BudgetAnalysis>> GetAnalysisAsync(int userId, DateTime from, DateTime to, int trendMonths)
    {
        // Sequential awaits: these accessors share the scoped DbContext, which cannot
        // serve concurrent queries.
        var transactions = (await transactionAccessor.GetByUserIdAsync(userId)).ToList();
        var budgetPlans = await budgetPlanAccessor.GetByUserIdAsync(userId);
        var activePlan = budgetPlans.FirstOrDefault(p => p.IsActive);

        var now = DateTime.UtcNow;

        // The window is an API concern, not a business one, so it is applied here.
        // `to` is inclusive of its whole day.
        var toExclusive = to.Date.AddDays(1);
        var windowTransactions = transactions
            .Where(t => t.OccurredAt >= from.Date && t.OccurredAt < toExclusive)
            .ToList();

        var analysis = new BudgetAnalysis(
            activePlan is null ? null : engine.AnalyzePlanMonth(transactions, activePlan, now),
            engine.SummarizeSpend(windowTransactions),
            engine.SummarizeByMonth(transactions, now, trendMonths));

        return Result<BudgetAnalysis>.Success(analysis);
    }
}
