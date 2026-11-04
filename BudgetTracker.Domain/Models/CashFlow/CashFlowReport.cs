using BudgetTracker.Domain.Models.Analysis;

namespace BudgetTracker.Domain.Models.CashFlow;

/// <summary>Bucket size for <see cref="CashFlowReport.Periods"/>. Weeks start on Sunday.</summary>
public enum CashFlowGrouping
{
    Day,
    Week,
    Month,
    Year
}

/// <summary>
/// What the cash-flow report covers. Every filter is optional; null means "no constraint".
/// A null <paramref name="From"/> means "from the first transaction" (the All range).
/// <paramref name="CompareFrom"/>/<paramref name="CompareTo"/> name the window behind
/// <see cref="CashFlowReport.PreviousTotals"/>; the client picks it because only it knows the preset.
/// </summary>
public record CashFlowQuery(
    DateTime? From,
    DateTime To,
    CashFlowGrouping GroupBy,
    DateTime? CompareFrom = null,
    DateTime? CompareTo = null,
    string? PlaidAccountId = null,
    int? CategoryId = null,
    string? Search = null);

/// <summary>
/// Money in and out of the user's accounts. Transfers between their own linked accounts are excluded.
/// Pending transactions are excluded from everything except <see cref="Pending"/>.
/// MoneyOut is net of refunds, so it can dip below zero in a period that was mostly refunds.
/// </summary>
public record CashFlowReport(
    /// <summary>Oldest first, one per bucket across the whole window, zero-filled so the axis has no gaps.</summary>
    IReadOnlyList<CashFlowPeriod> Periods,
    CashFlowTotals Totals,
    /// <summary>Null when no compare window was requested.</summary>
    CashFlowTotals? PreviousTotals,
    CashFlowTotals Pending,
    /// <summary>Income grouped by payee with digits stripped, largest first.</summary>
    IReadOnlyList<IncomeSource> IncomeSources,
    /// <summary>Net expense per category in the window (refunds subtracted).</summary>
    IReadOnlyList<CategorySpend> SpendByCategory,
    /// <summary>
    /// Checking + savings minus credit-card debt, as of the last sync. Null when balances do not apply:
    /// a category or search filter is set, or no depository/credit account has a synced balance.
    /// </summary>
    decimal? CurrentBalance);

/// <summary>
/// One bucket. <paramref name="Start"/> is the first day of the bucket.
/// <paramref name="Balance"/> is the end-of-bucket balance, reconstructed backwards from <see cref="CashFlowReport.CurrentBalance"/>; null whenever that is null.
/// </summary>
public record CashFlowPeriod(DateTime Start, decimal MoneyIn, decimal MoneyOut, decimal? Balance)
{
    public decimal Net => MoneyIn - MoneyOut;
}

public record CashFlowTotals(decimal MoneyIn, decimal MoneyOut)
{
    public decimal Net => MoneyIn - MoneyOut;
}

public record IncomeSource(string Source, decimal Amount);
