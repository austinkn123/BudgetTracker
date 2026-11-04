using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.MoneyMap;

namespace BudgetTracker.Domain.Engines;

/// <summary>
/// Plan vs monthly averages, plus what is coming: lump-sum bills projected from when they were last paid, and the
/// months a biweekly paycheck lands three times.
/// </summary>
public class MoneyMapEngine : IMoneyMapEngine
{
    private const string Income = "Income";
    private const string Expense = "Expense";
    private const string Annual = "Annual";
    private const int MaxMonths = 24;
    private const int TopPayeeCount = 3;
    private const int BiweeklyDays = 14;
    private const int RecentPaychecks = 6;
    private const string ExtraPaycheckLabel = "Extra paycheck";

    // A payment is the bill itself, not a fee or top-up, when it is at least this share of the plan's yearly amount.
    private const decimal BillShareOfAnnual = 0.25m;
    private static readonly int[] BillingPeriods = [1, 3, 6, 12];

    /// <inheritdoc />
    public Result<MoneyMap> Build(
        IEnumerable<BudgetPlan> plans, IReadOnlyList<Transaction> transactions, IEnumerable<Category> categories, DateTime today, int months)
    {
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(categories);

        if (months is < 1 or > MaxMonths)
            return Result<MoneyMap>.Failure($"months must be between 1 and {MaxMonths}");

        today = today.Date;
        var from = new DateTime(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var plan = plans.Where(p => p.IsActive).OrderByDescending(p => p.PlanMonth).FirstOrDefault();
        var names = categories.ToDictionary(c => c.Id, c => c.Name);
        var window = transactions.Where(t => t.OccurredAt.Date >= from && t.OccurredAt.Date <= today).ToList();

        var upcoming = plan is null
            ? []
            : ProjectBills(plan, transactions, names, today).Concat(ProjectExtraPaychecks(plan, transactions, today)).OrderBy(u => u.Date).ToList();

        return Result<MoneyMap>.Success(new MoneyMap(
            plan?.Name,
            from,
            today,
            months,
            plan?.NetIncomeMonthly ?? 0m,
            Round(window.Where(t => t.TransactionType == Income).Sum(t => t.Amount) / months),
            BuildLines(plan, window.Where(t => t.TransactionType == Expense).ToList(), names, months),
            upcoming));
    }

    /// <summary>Plan lines in plan order, then unplanned spend largest first. A category in several plan lines is one line.</summary>
    private static List<MoneyMapLine> BuildLines(BudgetPlan? plan, List<Transaction> spend, Dictionary<int, string> names, int months)
    {
        var rowsByCategory = spend.ToLookup(t => t.CategoryId);
        var planned = (plan?.Entries ?? [])
            .Where(e => e.LineType == Expense && e.CategoryId is not null)
            .OrderBy(e => e.SortOrder)
            .GroupBy(e => e.CategoryId!.Value)
            .Select(g =>
            {
                var first = g.First();
                var rows = rowsByCategory[g.Key].ToList();
                return new MoneyMapLine(g.Key, NameOf(g.Key, names), first.Bucket, first.Cadence,
                    g.Sum(e => e.MonthlyEquivalent), Average(rows, months), TopPayees(rows), first.Notes);
            })
            .ToList();

        var plannedIds = planned.Select(l => l.CategoryId).ToHashSet();
        var unplanned = rowsByCategory
            .Where(g => !plannedIds.Contains(g.Key))
            .Select(g => new MoneyMapLine(g.Key, g.Key is int id ? NameOf(id, names) : "Uncategorized", null, null, 0m,
                Average(g.ToList(), months), TopPayees(g.ToList()), null))
            .Where(l => l.ActualMonthly > 0)
            .OrderByDescending(l => l.ActualMonthly);

        return [.. planned, .. unplanned];
    }

    /// <summary>
    /// Each Annual plan line's bill, from its payments in the last 12 months. Two payments about six (or three, or one)
    /// months apart make a shorter cycle billed at the latest amount, as when switching to a 6-month car policy; any
    /// other payment recurs a year later.
    /// </summary>
    private static IEnumerable<UpcomingItem> ProjectBills(BudgetPlan plan, IReadOnlyList<Transaction> transactions, Dictionary<int, string> names, DateTime today)
    {
        // ponytail: a yearly bill paid a few days earlier than last year shows twice in the window; key bills by payee if
        // that ever happens.
        var yearAgo = today.AddMonths(-12);
        var horizon = today.AddMonths(12);

        foreach (var entry in plan.Entries.Where(e => e.LineType == Expense && e.Cadence == Annual && e.CategoryId is not null))
        {
            var bills = transactions
                .Where(t => t.TransactionType == Expense && t.CategoryId == entry.CategoryId &&
                            t.OccurredAt.Date > yearAgo && t.OccurredAt.Date <= today && -t.Amount >= entry.Amount * BillShareOfAnnual)
                .OrderBy(t => t.OccurredAt)
                .ToList();
            if (bills.Count == 0)
                continue;

            var last = bills[^1];
            var period = bills.Count >= 2 ? BillingPeriod(bills[^2].OccurredAt, last.OccurredAt) : 12;
            var due = period < 12
                ? Enumerable.Range(1, 12 / period).Select(k => (Date: last.OccurredAt.Date.AddMonths(k * period), Amount: -last.Amount))
                : bills.Select(b => (Date: b.OccurredAt.Date.AddMonths(12), Amount: -b.Amount));

            foreach (var (date, amount) in due.Where(d => d.Date > today && d.Date <= horizon))
                yield return new UpcomingItem(date, NameOf(entry.CategoryId!.Value, names), amount, UpcomingKind.Bill);
        }
    }

    /// <summary>
    /// For biweekly pay, the months ahead whose third payday is still to come. Pay is the plan's income categories; the
    /// paycheck before the latest is the next-newest one at least half its size, so a refund in between doesn't count.
    /// Each extra paycheck is the lower median of the recent ones, so a one-off bump (a reimbursement, a bonus) is not
    /// projected forward and a budget never counts on more than the usual check.
    /// </summary>
    private static IEnumerable<UpcomingItem> ProjectExtraPaychecks(BudgetPlan plan, IReadOnlyList<Transaction> transactions, DateTime today)
    {
        var incomeCategories = plan.Entries
            .Where(e => e.LineType == Income && e.CategoryId is not null)
            .Select(e => e.CategoryId!.Value)
            .ToHashSet();
        var pay = transactions
            .Where(t => t.TransactionType == Income && t.CategoryId is int id && incomeCategories.Contains(id) && t.OccurredAt.Date <= today)
            .OrderByDescending(t => t.OccurredAt)
            .ThenByDescending(t => t.Id)
            .ToList();
        if (pay.Count < 2)
            return [];

        var latest = pay[0];
        var paychecks = pay.Where(t => t.Amount >= latest.Amount / 2).Take(RecentPaychecks).ToList();
        if (paychecks.Count < 2 || Math.Abs((latest.OccurredAt.Date - paychecks[1].OccurredAt.Date).Days - BiweeklyDays) > 1)
            return [];

        // ponytail: after a raise the median lags until the new amount is the majority of the last few checks.
        var amounts = paychecks.Select(t => t.Amount).Order().ToList();
        var usual = amounts[(amounts.Count - 1) / 2];

        // Start a few paydays back so a month already under way counts the ones it has had.
        var horizon = today.AddMonths(12);
        var paydays = new List<DateTime>();
        for (var day = latest.OccurredAt.Date.AddDays(-3 * BiweeklyDays); day <= horizon; day = day.AddDays(BiweeklyDays))
            paydays.Add(day);

        return paydays
            .GroupBy(d => (d.Year, d.Month))
            .Where(g => g.Count() >= 3 && g.ElementAt(2) > today)
            .Select(g => new UpcomingItem(g.ElementAt(2), ExtraPaycheckLabel, usual, UpcomingKind.ExtraPaycheck))
            .ToList();
    }

    /// <summary>Calendar months between two payments, snapped to the nearest common billing period.</summary>
    private static int BillingPeriod(DateTime earlier, DateTime later)
    {
        var months = (later.Year - earlier.Year) * 12 + later.Month - earlier.Month;
        return BillingPeriods.MinBy(p => Math.Abs(p - months));
    }

    private static decimal Average(List<Transaction> rows, int months) => Round(-rows.Sum(t => t.Amount) / months);

    private static IReadOnlyList<string> TopPayees(List<Transaction> rows) => rows
        .Where(t => !string.IsNullOrWhiteSpace(t.Payee))
        .GroupBy(t => t.Payee!)
        .Select(g => (Payee: g.Key, Spend: -g.Sum(t => t.Amount)))
        .Where(p => p.Spend > 0)
        .OrderByDescending(p => p.Spend)
        .Take(TopPayeeCount)
        .Select(p => p.Payee)
        .ToList();

    private static string NameOf(int categoryId, Dictionary<int, string> names) =>
        names.GetValueOrDefault(categoryId) ?? $"Category {categoryId}";

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
