using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Engines;

public class BudgetPlanEngine : IBudgetPlanEngine
{
    private static readonly HashSet<string> ValidLineTypes = ["Income", "Expense"];
    private static readonly HashSet<string> ValidBuckets = ["Core", "Buffer"];
    private static readonly HashSet<string> ValidCadences = ["Monthly", "Annual"];

    public string? ValidateBudgetPlan(BudgetPlan budgetPlan)
    {
        if (budgetPlan.UserId <= 0)
            return "A valid user is required";

        if (string.IsNullOrWhiteSpace(budgetPlan.Name))
            return "Plan name is required";

        if (budgetPlan.Name.Length > 100)
            return "Plan name must be 100 characters or fewer";

        if (budgetPlan.PlanMonth == default)
            return "Plan month is required";

        if (budgetPlan.NetIncomeMonthly < 0)
            return "Net income must be zero or greater";

        return ValidateEntries(budgetPlan.Entries);
    }

    public void NormalizeForPersistence(BudgetPlan budgetPlan)
    {
        budgetPlan.Name = budgetPlan.Name.Trim();
        budgetPlan.PlanMonth = new DateTime(budgetPlan.PlanMonth.Year, budgetPlan.PlanMonth.Month, 1);

        foreach (var entry in budgetPlan.Entries)
        {
            entry.LineType = NormalizeValue(entry.LineType, ValidLineTypes);
            entry.Bucket = NormalizeValue(entry.Bucket, ValidBuckets);
            entry.Cadence = NormalizeValue(entry.Cadence, ValidCadences);
            entry.MonthlyEquivalent = entry.Cadence == "Annual"
                ? Math.Round(entry.Amount / 12m, 2, MidpointRounding.AwayFromZero)
                : entry.Amount;
        }
    }

    private static string? ValidateEntries(IEnumerable<BudgetPlanEntry> entries)
    {
        var entryNumber = 0;
        foreach (var entry in entries)
        {
            entryNumber++;

            if (!ValidLineTypes.Contains(entry.LineType))
                return $"Entry {entryNumber}: line type must be Income or Expense";

            if (!ValidBuckets.Contains(entry.Bucket))
                return $"Entry {entryNumber}: bucket must be Core or Buffer";

            if (!ValidCadences.Contains(entry.Cadence))
                return $"Entry {entryNumber}: cadence must be Monthly or Annual";

            if (entry.Amount < 0)
                return $"Entry {entryNumber}: amount must be zero or greater";

            if (entry.MonthlyEquivalent < 0)
                return $"Entry {entryNumber}: monthly equivalent must be zero or greater";

            if (entry.CategoryId is <= 0)
                return $"Entry {entryNumber}: category ID must be greater than zero when provided";

            if (!string.IsNullOrEmpty(entry.Notes) && entry.Notes.Length > 500)
                return $"Entry {entryNumber}: notes must be 500 characters or fewer";
        }

        return null;
    }

    private static string NormalizeValue(string? value, HashSet<string> validValues)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        var canonical = validValues.FirstOrDefault(v => string.Equals(v, trimmed, StringComparison.OrdinalIgnoreCase));
        return canonical ?? trimmed;
    }
}