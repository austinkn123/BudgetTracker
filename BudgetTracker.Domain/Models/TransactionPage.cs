namespace BudgetTracker.Domain.Models;

/// <summary>
/// Keyset position in the ledger's newest-first order (OccurredAt DESC, Id DESC): the last row a page returned.
/// The next page starts strictly after it.
/// </summary>
/// <param name="OccurredAt">The last returned row's <see cref="Transaction.OccurredAt"/>.</param>
/// <param name="Id">The last returned row's <see cref="Transaction.Id"/>; breaks ties between rows on the same instant.</param>
public record TransactionCursor(DateTime OccurredAt, int Id);

/// <summary>One page of the ledger, newest first.</summary>
/// <param name="Items">The rows on this page, in OccurredAt DESC, Id DESC order.</param>
/// <param name="NextCursor">Opaque cursor for the next page, or null when this is the last page.</param>
public record TransactionPage(IReadOnlyList<Transaction> Items, string? NextCursor);

/// <summary>Ledger-wide counters the transactions page shows without loading the ledger.</summary>
/// <param name="UncategorizedCount">Rows with no category that are not a paired Transfer.</param>
/// <param name="PendingByAccount">Sum of pending amounts per Plaid account.</param>
public record TransactionSummary(int UncategorizedCount, IReadOnlyList<PendingAccountTotal> PendingByAccount);

/// <summary>The signed total of one Plaid account's pending rows.</summary>
/// <param name="PlaidAccountId">Plaid's raw account id.</param>
/// <param name="Amount">Sum of the pending rows' signed amounts.</param>
public record PendingAccountTotal(string PlaidAccountId, decimal Amount);

/// <summary>How much one category is used.</summary>
/// <param name="CategoryId">The category.</param>
/// <param name="TransactionCount">All-time count of the user's rows in the category.</param>
/// <param name="InflowSince">Sum of positive amounts dated on or after the requested instant.</param>
/// <param name="OutflowSince">Sum of negative amounts dated on or after the requested instant (zero or negative).</param>
public record CategoryUsage(int CategoryId, int TransactionCount, decimal InflowSince, decimal OutflowSince);
