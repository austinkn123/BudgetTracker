using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Domain.Engines;

/// <inheritdoc />
public class SandboxSeedEngine : ISandboxSeedEngine
{
    private const string IncomeDescription = "Employer Direct Deposit";
    private const string UncategorizedName = "General";

    /// <summary>
    /// Categories whose spend arrives as many small purchases rather than one payment. Matched as a
    /// case-insensitive substring of the category name, so "Food (Groceries + Eating Out)" is caught
    /// by "food". Anything unmatched is treated as a single monthly bill.
    /// </summary>
    private static readonly (string Keyword, int PerMonth, string Merchant)[] VariableSpend =
    [
        ("food",      8, "H-E-B"),
        ("grocer",    8, "H-E-B"),
        ("eating",    6, "Chipotle"),
        ("dining",    6, "Chipotle"),
        ("gas",       4, "Shell"),
        ("toll",      4, "Shell"),
        ("travel",    2, "Southwest Airlines"),
        ("misc",      3, "GitHub")
    ];

    /// <summary>Fixed bills, matched the same way, so the seeded payee reads like the real one.</summary>
    private static readonly (string Keyword, string Merchant)[] FixedMerchants =
    [
        ("mortgage",  "First National Mortgage"),
        ("rent",      "First National Mortgage"),
        ("insurance", "GEICO"),
        ("utilit",    "City Utilities"),
        ("bill",      "City Utilities"),
        ("health",    "Baylor Scott & White"),
        ("dental",    "Baylor Scott & White"),
        ("car",       "Firestone Complete Auto"),
        ("home",      "The Home Depot")
    ];

    /// <inheritdoc />
    public string? ValidateMonths(int months) =>
        months is < 1 or > 12 ? "Months must be between 1 and 12" : null;

    /// <inheritdoc />
    public IReadOnlyList<SandboxSeedLine> BuildLines(BudgetPlan plan, IReadOnlyDictionary<int, string> categoryNames)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(categoryNames);

        return plan.Entries
            .Where(e => e.LineType == "Expense" && e.MonthlyEquivalent > 0)
            .Select(e => BuildLine(
                e.CategoryId is int id && categoryNames.TryGetValue(id, out var name) ? name : UncategorizedName,
                e.MonthlyEquivalent))
            .ToList();
    }

    /// <inheritdoc />
    public string BuildConfig(IReadOnlyList<SandboxSeedLine> lines, decimal monthlyIncome, DateOnly from, DateOnly through) =>
        SandboxSeedBuilder.Build(lines, monthlyIncome, IncomeDescription, from, through);

    /// <inheritdoc />
    public PlaidItem? SelectItemToReplace(IReadOnlyList<PlaidItem> activeItems)
    {
        ArgumentNullException.ThrowIfNull(activeItems);

        return activeItems
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .FirstOrDefault();
    }

    private static SandboxSeedLine BuildLine(string categoryName, decimal monthlyAmount)
    {
        foreach (var (keyword, perMonth, merchant) in VariableSpend)
        {
            if (categoryName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return new SandboxSeedLine(merchant, monthlyAmount, perMonth);
        }

        foreach (var (keyword, merchant) in FixedMerchants)
        {
            if (categoryName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return new SandboxSeedLine(merchant, monthlyAmount, 1);
        }

        // Unmatched: keep the category's own name so the row is still self-explanatory.
        return new SandboxSeedLine(categoryName, monthlyAmount, 1);
    }
}
