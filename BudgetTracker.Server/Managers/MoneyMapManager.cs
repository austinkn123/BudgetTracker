using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.MoneyMap;

namespace BudgetTracker.Server.Managers;

public class MoneyMapManager(
    IMoneyMapEngine engine,
    ITransactionAccessor transactionAccessor,
    IBudgetPlanAccessor budgetPlanAccessor,
    ICategoryAccessor categoryAccessor) : IMoneyMapManager
{
    /// <inheritdoc />
    public async Task<Result<MoneyMap>> GetAsync(int userId, int months)
    {
        var today = DateTime.UtcNow.Date;

        // Sequential awaits: these accessors share the scoped DbContext, which cannot run two queries at once.
        var plans = await budgetPlanAccessor.GetByUserIdAsync(userId);
        var history = engine.DescribeHistory(plans, today, months);
        if (!history.IsSuccess)
            return Result<MoneyMap>.Failure(history.Error!);

        var (from, payCategoryIds, recentPaychecks) = history.Value!;
        var transactions = (await transactionAccessor.GetFilteredAsync(userId, new TransactionFilter(From: from, To: today))).ToList();

        // The extra-paycheck projection reads the newest few paychecks wherever they fall, so those few are fetched on
        // their own rather than widening the main read to all history.
        if (payCategoryIds.Count > 0)
        {
            var latest = await transactionAccessor.GetNewestIncomeAsync(userId, payCategoryIds, today, minAmount: null, take: 1);
            if (latest.Count > 0)
            {
                var paychecks = await transactionAccessor.GetNewestIncomeAsync(
                    userId, payCategoryIds, today, engine.PaycheckFloor(latest[0].Amount), recentPaychecks);
                transactions = transactions.Concat(latest).Concat(paychecks).DistinctBy(t => t.Id).ToList();
            }
        }

        var categories = await categoryAccessor.GetByUserIdAsync(userId);
        return engine.Build(plans, transactions, categories, today, months);
    }
}
