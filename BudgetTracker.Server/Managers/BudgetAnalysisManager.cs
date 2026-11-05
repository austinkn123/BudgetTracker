using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.Analysis;

namespace BudgetTracker.Server.Managers;

public class BudgetAnalysisManager(
    IBudgetAnalysisEngine engine,
    ITransactionAccessor transactionAccessor,
    IBudgetPlanAccessor budgetPlanAccessor) : IBudgetAnalysisManager
{
    public async Task<Result<BudgetAnalysis>> GetAnalysisAsync(int userId, DateTime from, DateTime to, int trendMonths)
    {
        if (engine.ValidateTrendMonths(trendMonths) is string error)
            return Result<BudgetAnalysis>.Failure(error);

        var now = DateTime.UtcNow;

        // The analysed month follows the requested window rather than the wall clock, so browsing
        // to June returns June's plan-vs-actual instead of today's. The engine derives the month it
        // analyses from the instant it is handed, so choosing the right instant here is the whole
        // change — for a month already finished we pass its last day, which makes daysElapsed cover
        // the full month and pacing read as final; for the current month we clamp to now so live
        // pacing is untouched.
        // A future `to` (the ALL range sends 9999-12-31) means the current month; clamping first keeps AddMonths in range.
        var lastDay = to < now ? to : now;
        var requestedMonth = new DateTime(lastDay.Year, lastDay.Month, 1);
        var lastInstantOfRequestedMonth = requestedMonth.AddMonths(1).AddDays(-1);
        var asOf = lastInstantOfRequestedMonth < now ? lastInstantOfRequestedMonth : now;
        var analyzedMonth = new DateTime(asOf.Year, asOf.Month, 1);

        // Fetch only what the three figures read: the window, the analysed month in full, and the trend months before
        // it. Rows outside that span used to be loaded just to seed zeroed trend series for every category ever used;
        // the distinct category ids now come from the database instead.
        var trendStart = engine.TrendStart(asOf, trendMonths);
        var analyzedMonthEnd = analyzedMonth.AddMonths(1).AddDays(-1);
        var fetch = new TransactionFilter(
            From: from.Date < trendStart ? from.Date : trendStart,
            To: to.Date > analyzedMonthEnd ? to.Date : analyzedMonthEnd);

        // Sequential awaits: these accessors share the scoped DbContext, which cannot
        // serve concurrent queries.
        var transactions = (await transactionAccessor.GetFilteredAsync(userId, fetch)).ToList();
        var categoryIdsInUse = await transactionAccessor.GetCategoryIdsInUseAsync(userId);
        var budgetPlans = await budgetPlanAccessor.GetByUserIdAsync(userId);

        // Plans roll forward: PlanMonth is the month a plan takes effect, so the one governing the
        // analysed month is the NEWEST active plan dated on or before it. Picking the first active
        // plan regardless of date meant a stale plan reported its own month forever.
        var governingPlan = budgetPlans
            .Where(p => p.IsActive && p.PlanMonth <= analyzedMonth)
            .OrderByDescending(p => p.PlanMonth)
            .FirstOrDefault();

        // The window is an API concern, not a business one, so it is applied here.
        var windowTransactions = transactions
            .Where(t => t.OccurredAt >= from.Date && t.OccurredAt.Date <= to.Date)
            .ToList();

        var analysis = new BudgetAnalysis(
            governingPlan is null ? null : engine.AnalyzeMonth(transactions, governingPlan, asOf),
            engine.SummarizeSpend(windowTransactions),
            engine.SummarizeByMonth(transactions, asOf, trendMonths, categoryIdsInUse));

        return Result<BudgetAnalysis>.Success(analysis);
    }
}
