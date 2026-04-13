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

        return ValidateLines(budgetPlan.Lines);
    }

    public void NormalizeForPersistence(BudgetPlan budgetPlan)
    {
        budgetPlan.Name = budgetPlan.Name.Trim();
        budgetPlan.PlanMonth = new DateTime(budgetPlan.PlanMonth.Year, budgetPlan.PlanMonth.Month, 1);

        foreach (var line in budgetPlan.Lines)
        {
            line.LineType = NormalizeValue(line.LineType, ValidLineTypes);
            line.Bucket = NormalizeValue(line.Bucket, ValidBuckets);
            line.Cadence = NormalizeValue(line.Cadence, ValidCadences);
            line.MonthlyEquivalent = line.Cadence == "Annual"
                ? Math.Round(line.Amount / 12m, 2, MidpointRounding.AwayFromZero)
                : line.Amount;
        }
    }

    private static string? ValidateLines(IEnumerable<BudgetPlanLine> lines)
    {
        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;

            if (!ValidLineTypes.Contains(line.LineType))
                return $"Line {lineNumber}: line type must be Income or Expense";

            if (!ValidBuckets.Contains(line.Bucket))
                return $"Line {lineNumber}: bucket must be Core or Buffer";

            if (!ValidCadences.Contains(line.Cadence))
                return $"Line {lineNumber}: cadence must be Monthly or Annual";

            if (line.Amount < 0)
                return $"Line {lineNumber}: amount must be zero or greater";

            if (line.MonthlyEquivalent < 0)
                return $"Line {lineNumber}: monthly equivalent must be zero or greater";

            if (line.CategoryId is <= 0)
                return $"Line {lineNumber}: category ID must be greater than zero when provided";

            if (!string.IsNullOrEmpty(line.Notes) && line.Notes.Length > 500)
                return $"Line {lineNumber}: notes must be 500 characters or fewer";
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