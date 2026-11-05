using System.Text.RegularExpressions;
using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.Analysis;
using BudgetTracker.Domain.Models.CashFlow;
using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Domain.Engines;

/// <inheritdoc />
public partial class CashFlowEngine : ICashFlowEngine
{
    private const string Income = "Income";
    private const string Expense = "Expense";
    private const string OtherSource = "Other";

    /// <inheritdoc />
    public Result<CashFlowReport> BuildReport(
        CashFlowQuery query,
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<PlaidAccount> accounts,
        IReadOnlyDictionary<string, decimal> postedAfterToByAccount,
        DateTime today)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(postedAfterToByAccount);

        var to = query.To.Date;
        if (query.From?.Date > to)
            return Result<CashFlowReport>.Failure("From must be on or before To");

        // All (no From) starts at the first transaction; with none there is nothing to chart.
        var from = query.From?.Date ?? transactions.Where(t => t.OccurredAt.Date <= to).Min(t => (DateTime?)t.OccurredAt.Date);
        var window = from is DateTime start ? InWindow(transactions, start, to) : [];
        var posted = window.Where(t => !t.IsPending).ToList();

        var balanceAccounts = BalanceAccounts(query, accounts);
        decimal? currentBalance = balanceAccounts?.Sum(SignedBalance);

        var compare = query is { CompareFrom: DateTime compareFrom, CompareTo: DateTime compareTo }
            ? Totals(InWindow(transactions, compareFrom.Date, compareTo.Date).Where(t => !t.IsPending))
            : null;

        return Result<CashFlowReport>.Success(new CashFlowReport(
            from is DateTime first ? Periods(query.GroupBy, first, to, posted, transactions, postedAfterToByAccount, balanceAccounts, currentBalance, today.Date) : [],
            Totals(posted),
            compare,
            Totals(window.Where(t => t.IsPending)),
            IncomeSources(posted),
            posted
                .Where(t => t.TransactionType == Expense)
                .GroupBy(t => t.CategoryId)
                .Select(g => new CategorySpend(g.Key, -g.Sum(t => t.Amount)))
                .ToList(),
            currentBalance));
    }

    private static List<CashFlowPeriod> Periods(
        CashFlowGrouping groupBy, DateTime from, DateTime to, List<Transaction> posted,
        IReadOnlyList<Transaction> allRows, IReadOnlyDictionary<string, decimal> postedAfterToByAccount,
        List<PlaidAccount>? balanceAccounts, decimal? currentBalance, DateTime today)
    {
        var byBucket = posted.ToLookup(t => BucketStart(t.OccurredAt.Date, groupBy));

        // ponytail: balances are reconstructed backwards from the last synced balance, so one missing or mis-dated
        // transaction drifts every earlier bucket. Snapshotting balances per sync is the upgrade path.
        // Every bucket's balance is as of To or earlier, so everything after To is undone in full: the database sums
        // those rows per account rather than shipping them here. Rows after To that were fetched for a compare window are
        // skipped so they are not undone twice.
        var balanceAccountIds = balanceAccounts?.Select(a => a.PlaidAccountId).ToHashSet() ?? [];
        var postedAfterTo = balanceAccountIds.Sum(id => postedAfterToByAccount.GetValueOrDefault(id));
        var balanceRows = allRows
            .Where(t => !t.IsPending && t.PlaidAccountId is not null && balanceAccountIds.Contains(t.PlaidAccountId) &&
                        t.OccurredAt.Date <= to)
            .ToList();

        var periods = new List<CashFlowPeriod>();
        for (var bucket = BucketStart(from, groupBy); bucket <= to; bucket = NextBucket(bucket, groupBy))
        {
            // A bucket cut short by To (or still running today) reports the balance on that day, not at its natural end.
            var bucketEnd = NextBucket(bucket, groupBy).AddDays(-1);
            var asOf = new[] { bucketEnd, to, today }.Min();
            var totals = Totals(byBucket[bucket]);
            periods.Add(new CashFlowPeriod(
                bucket,
                totals.MoneyIn,
                totals.MoneyOut,
                currentBalance - postedAfterTo - balanceRows.Where(t => t.OccurredAt.Date > asOf).Sum(t => t.Amount)));
        }

        return periods;
    }

    /// <summary>Money in is posted Income; money out is posted Expense net of refunds. Transfers and adjustments never count.</summary>
    private static CashFlowTotals Totals(IEnumerable<Transaction> rows)
    {
        decimal moneyIn = 0, moneyOut = 0;
        foreach (var t in rows)
        {
            if (t.TransactionType == Income) moneyIn += t.Amount;
            else if (t.TransactionType == Expense) moneyOut -= t.Amount;
        }

        return new CashFlowTotals(moneyIn, moneyOut);
    }

    private static List<IncomeSource> IncomeSources(IEnumerable<Transaction> posted) =>
        posted
            .Where(t => t.TransactionType == Income)
            .GroupBy(t => SourceName(t.Payee), StringComparer.OrdinalIgnoreCase)
            .Select(g => new IncomeSource(g.Key, g.Sum(t => t.Amount)))
            .OrderByDescending(s => s.Amount)
            .ThenBy(s => s.Source, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>"ACME PAYROLL 0412" and "Acme Payroll 0419" are one source: digits stripped, whitespace collapsed.</summary>
    private static string SourceName(string? payee)
    {
        var name = Whitespace().Replace(Digits().Replace(payee ?? string.Empty, string.Empty), " ").Trim();
        return name.Length == 0 ? OtherSource : name;
    }

    /// <summary>
    /// Cash and card accounts with a synced balance, narrowed to the account filter. Null when balances do not apply: a
    /// category or search filter is set (a balance cannot be split that way), or no account qualifies.
    /// </summary>
    private static List<PlaidAccount>? BalanceAccounts(CashFlowQuery query, IReadOnlyList<PlaidAccount> accounts)
    {
        if (query.CategoryId is not null || !string.IsNullOrWhiteSpace(query.Search))
            return null;

        var qualifying = accounts
            .Where(a => a.CurrentBalance is not null && PlaidAccountTypes.IsCash(a.AccountType))
            .Where(a => string.IsNullOrWhiteSpace(query.PlaidAccountId) || a.PlaidAccountId == query.PlaidAccountId)
            .ToList();

        return qualifying.Count == 0 ? null : qualifying;
    }

    /// <summary>A card's balance is what is owed, so it counts against the total.</summary>
    private static decimal SignedBalance(PlaidAccount account) =>
        PlaidAccountTypes.IsCredit(account.AccountType) ? -account.CurrentBalance!.Value : account.CurrentBalance!.Value;

    private static List<Transaction> InWindow(IEnumerable<Transaction> rows, DateTime from, DateTime to) =>
        rows.Where(t => t.OccurredAt.Date >= from && t.OccurredAt.Date <= to).ToList();

    private static DateTime BucketStart(DateTime date, CashFlowGrouping groupBy) => groupBy switch
    {
        CashFlowGrouping.Day => date,
        CashFlowGrouping.Week => date.AddDays(-(int)date.DayOfWeek),
        CashFlowGrouping.Month => new DateTime(date.Year, date.Month, 1),
        CashFlowGrouping.Year => new DateTime(date.Year, 1, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, null)
    };

    private static DateTime NextBucket(DateTime bucketStart, CashFlowGrouping groupBy) => groupBy switch
    {
        CashFlowGrouping.Day => bucketStart.AddDays(1),
        CashFlowGrouping.Week => bucketStart.AddDays(7),
        CashFlowGrouping.Month => bucketStart.AddMonths(1),
        CashFlowGrouping.Year => bucketStart.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, null)
    };

    [GeneratedRegex(@"\d")]
    private static partial Regex Digits();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
