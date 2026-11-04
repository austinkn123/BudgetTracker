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
        // One read covers the window, the compare window (which usually starts first) and every row after To,
        // which the engine needs to walk balances back from today. No From means All, so nothing is cut off.
        var fetchFrom = query.From is DateTime from && query.CompareFrom is DateTime compareFrom && compareFrom < from
            ? compareFrom
            : query.From;
        var filter = new TransactionFilter(
            From: fetchFrom, Search: query.Search, PlaidAccountId: query.PlaidAccountId, CategoryId: query.CategoryId);

        // Sequential awaits: both accessors share the scoped DbContext.
        var transactions = (await transactionAccessor.GetFilteredAsync(userId, filter)).ToList();
        var items = await itemAccessor.GetAllActiveByUserIdAsync(userId);

        return engine.BuildReport(query, transactions, items.SelectMany(i => i.Accounts).ToList(), DateTime.UtcNow.Date);
    }
}
