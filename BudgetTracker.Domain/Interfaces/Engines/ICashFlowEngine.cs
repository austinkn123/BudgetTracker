using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.CashFlow;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>Pure cash-flow arithmetic: bucketing, totals, income sources, and reconstructed balances. No I/O.</summary>
public interface ICashFlowEngine
{
    /// <summary>
    /// Build the report for <paramref name="query"/>. Only Income and Expense rows count (Transfer and Adjustment never do),
    /// and pending rows count only toward <see cref="CashFlowReport.Pending"/>.
    /// </summary>
    /// <param name="query">Window, grouping, compare window and filters. Dates compare on whole days.</param>
    /// <param name="transactions">
    /// Every row matching the query's filters from the earlier of From/CompareFrom (or all time) through the later of
    /// To/CompareTo. Rows dated after To are never used for balances; <paramref name="postedAfterToByAccount"/> covers them.
    /// </param>
    /// <param name="accounts">The user's linked Plaid accounts with their last synced balances.</param>
    /// <param name="postedAfterToByAccount">
    /// Sum of signed amounts of the user's posted rows dated after To, by Plaid account id, unfiltered: what period
    /// balances undo, beyond the in-window rows, to walk back from today's balance.
    /// </param>
    /// <param name="today">The current date; a bucket ending after it takes today's balance.</param>
    /// <returns>The report, or a failure when From is after To.</returns>
    Result<CashFlowReport> BuildReport(
        CashFlowQuery query,
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<PlaidAccount> accounts,
        IReadOnlyDictionary<string, decimal> postedAfterToByAccount,
        DateTime today);
}
