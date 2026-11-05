using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.CashFlow;

namespace BudgetTracker.Server.Managers;

/// <inheritdoc />
public class CashFlowManager(
    ICashFlowEngine engine,
    ITransactionAccessor transactionAccessor,
    IPlaidItemAccessor itemAccessor) : ICashFlowManager
{
    /// <inheritdoc />
    public async Task<Result<CashFlowReport>> GetReportAsync(int userId, CashFlowQuery query)
    {
        // One read covers the window and the compare window (either may start or end first). Rows after To only matter
        // to period balances, which need just their per-account sum, so the database aggregates them instead.
        var fetchFrom = query.From is DateTime from && query.CompareFrom is DateTime compareFrom && compareFrom < from
            ? compareFrom
            : query.From;
        var fetchTo = query.CompareTo is DateTime compareTo && compareTo > query.To ? compareTo : query.To;
        var filter = new TransactionFilter(
            From: fetchFrom, To: fetchTo, Search: query.Search, PlaidAccountId: query.PlaidAccountId, CategoryId: query.CategoryId);

        // Sequential awaits: both accessors share the scoped DbContext.
        var transactions = (await transactionAccessor.GetFilteredAsync(userId, filter)).ToList();
        var postedAfterTo = await transactionAccessor.GetPostedSumByPlaidAccountAfterAsync(userId, query.To);
        var items = await itemAccessor.GetAllActiveByUserIdAsync(userId);

        return engine.BuildReport(query, transactions, items.SelectMany(i => i.Accounts).ToList(), postedAfterTo, DateTime.UtcNow.Date);
    }
}
