using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.Analysis;

namespace BudgetTracker.Domain.Engines;

public class BudgetAnalysisEngine : IBudgetAnalysisEngine
{
    private const string Expense = "Expense";
    private const string Income = "Income";
    private const string CoreBucket = "Core";
    private const string BufferBucket = "Buffer";

    /// <summary>Spending is "off pace" once it drifts this far from elapsed time, in either direction.</summary>
    private const decimal PacingTolerance = 0.05m;

    // Uncategorized transactions carry a null CategoryId. Grouping is done with LINQ
    // (ToLookup/GroupBy) rather than Dictionary throughout, because Dictionary rejects null keys.

    public PlanPerformance AnalyzePlanMonth(IReadOnlyList<Transaction> transactions, BudgetPlan plan, DateTime now)
    {
        var monthTxns = transactions
            .Where(t => t.OccurredAt.Year == plan.PlanMonth.Year && t.OccurredAt.Month == plan.PlanMonth.Month)
            .ToList();
        var monthExpenses = monthTxns.Where(t => t.TransactionType == Expense).ToList();

        // Planned side: expense lines only, split by category and by bucket.
        // Plan entries always carry a concrete category id (or none at all), so a Dictionary is safe here.
        var plannedByCategory = new Dictionary<int, decimal>();
        var bucketByCategory = new Dictionary<int, string>();
        decimal plannedTotal = 0, plannedCore = 0, plannedBuffer = 0;
        foreach (var line in plan.Entries)
        {
            if (line.LineType != Expense) continue;

            plannedTotal += line.MonthlyEquivalent;
            if (line.Bucket == CoreBucket) plannedCore += line.MonthlyEquivalent;
            else plannedBuffer += line.MonthlyEquivalent;

            if (line.CategoryId is int categoryId)
            {
                plannedByCategory[categoryId] = plannedByCategory.GetValueOrDefault(categoryId) + line.MonthlyEquivalent;
                bucketByCategory[categoryId] = line.Bucket;
            }
        }

        // Actual side. BUD-18: expense amounts arrive signed, so accumulate magnitude.
        var actualByCategory = monthExpenses.ToLookup(t => t.CategoryId);

        decimal actualTotal = 0, actualCore = 0, actualBuffer = 0;
        foreach (var t in monthExpenses)
        {
            var magnitude = Math.Abs(t.Amount);
            actualTotal += magnitude;

            // Spend in a category the plan doesn't budget for falls back to Buffer.
            var bucket = t.CategoryId is int id ? bucketByCategory.GetValueOrDefault(id) : null;
            if (bucket == CoreBucket) actualCore += magnitude;
            else actualBuffer += magnitude;
        }

        var categoryIds = new HashSet<int?>(actualByCategory.Select(g => g.Key));
        foreach (var id in plannedByCategory.Keys) categoryIds.Add(id);

        var byCategory = categoryIds
            .Select(id => new CategoryPerformance(
                id,
                id is int planned ? plannedByCategory.GetValueOrDefault(planned) : 0m,
                actualByCategory[id].Sum(t => Math.Abs(t.Amount))))
            .ToList();

        var byBucket = new List<BucketPerformance>
        {
            new(CoreBucket, plannedCore, actualCore),
            new(BufferBucket, plannedBuffer, actualBuffer)
        };

        return new PlanPerformance(
            new AnalyzedPlan(plan.Id, plan.Name, plan.PlanMonth),
            ComputePacing(plan.PlanMonth, plannedTotal, actualTotal, now),
            byCategory,
            byBucket,
            monthTxns.Where(t => t.TransactionType == Income).Sum(t => t.Amount),
            actualTotal);
    }

    public IReadOnlyList<CategorySpend> SummarizeSpend(IReadOnlyList<Transaction> transactions) =>
        transactions
            .Where(t => t.TransactionType == Expense)
            .GroupBy(t => t.CategoryId)
            .Select(g => new CategorySpend(g.Key, g.Sum(t => Math.Abs(t.Amount))))
            .ToList();

    public IReadOnlyList<CategoryMonthSpend> SummarizeByMonth(IReadOnlyList<Transaction> transactions, DateTime now, int months)
    {
        if (months <= 0) return [];

        // Oldest -> newest, ending with the month containing `now`.
        var window = Enumerable.Range(1 - months, months)
            .Select(offset => new DateTime(now.Year, now.Month, 1).AddMonths(offset))
            .ToList();

        // Every category that appears anywhere gets the full window, so trend series are gap-free.
        return transactions
            .GroupBy(t => t.CategoryId)
            .SelectMany(categoryGroup =>
            {
                var byMonth = categoryGroup.ToLookup(t => new DateTime(t.OccurredAt.Year, t.OccurredAt.Month, 1));
                return window.Select(month => new CategoryMonthSpend(
                    categoryGroup.Key,
                    month,
                    byMonth[month].Where(t => t.TransactionType == Income).Sum(t => t.Amount),
                    byMonth[month].Where(t => t.TransactionType != Income).Sum(t => Math.Abs(t.Amount))));
            })
            .ToList();
    }

    private static PeriodPacing ComputePacing(DateTime planMonth, decimal plannedExpenses, decimal actualExpenses, DateTime now)
    {
        var daysInMonth = DateTime.DaysInMonth(planMonth.Year, planMonth.Month);
        var monthStart = new DateTime(planMonth.Year, planMonth.Month, 1);
        var monthEndExclusive = monthStart.AddMonths(1);

        var daysElapsed = now < monthStart ? 0
            : now >= monthEndExclusive ? daysInMonth
            : now.Day;

        var daysPct = (decimal)daysElapsed / daysInMonth;
        var spentPct = plannedExpenses > 0 ? actualExpenses / plannedExpenses : 0;
        // With no elapsed days there is no run rate to extrapolate from.
        var projectedEnd = daysElapsed <= 0 ? actualExpenses : actualExpenses / daysElapsed * daysInMonth;
        var pacingDelta = spentPct - daysPct;

        var status = pacingDelta <= -PacingTolerance ? PacingStatus.Ahead
            : pacingDelta >= PacingTolerance ? PacingStatus.Behind
            : PacingStatus.OnTrack;

        var remaining = plannedExpenses - actualExpenses;
        var daysLeft = Math.Max(daysInMonth - daysElapsed, 0);

        return new PeriodPacing(
            daysElapsed, daysInMonth, daysPct, spentPct, projectedEnd, pacingDelta, status,
            plannedExpenses, actualExpenses, remaining,
            daysLeft > 0 ? remaining / daysLeft : 0);
    }
}
