using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
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
        // Sequential awaits: these accessors share the scoped DbContext, which cannot run two queries at once.
        var transactions = (await transactionAccessor.GetByUserIdAsync(userId)).ToList();
        var plans = await budgetPlanAccessor.GetByUserIdAsync(userId);
        var categories = await categoryAccessor.GetByUserIdAsync(userId);

        return engine.Build(plans, transactions, categories, DateTime.UtcNow.Date, months);
    }
}
