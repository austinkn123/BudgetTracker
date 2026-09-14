using System.Text.Json;

namespace BudgetTracker.Domain.Plaid;

/// <summary>One recurring outgoing line to synthesise, already resolved to a display name.</summary>
/// <param name="Description">Merchant text Plaid will echo back as the transaction name.</param>
/// <param name="MonthlyAmount">Total to spend on this line per month (positive magnitude).</param>
/// <param name="PerMonth">How many transactions to split that total across.</param>
public sealed record SandboxSeedLine(string Description, decimal MonthlyAmount, int PerMonth);

/// <summary>
/// Builds a Plaid <c>user_custom</c> Sandbox configuration (BUD-3, dev tooling only).
///
/// Plaid's default Sandbox user returns a fixed fixture set — "Tartan Bank", "Tectra Inc",
/// "CD DEPOSIT .INITIAL." — with amounts bearing no relation to a real budget, which makes the
/// dashboard unreadable. A custom user lets us hand Plaid the exact transactions to return, so the
/// numbers reconcile against the user's own plan.
///
/// The output is passed as <c>options.override_password</c> to <c>/sandbox/public_token/create</c>
/// alongside <c>override_username = "user_custom"</c>.
///
/// Documented limits: roughly 250 transactions / 55 kb per configuration, and at most ten accounts.
/// <see cref="MaxTransactions"/> keeps us clear of the first.
/// </summary>
public static class SandboxSeedBuilder
{
    /// <summary>Plaid rejects configurations beyond roughly 250 transactions; stop short of it.</summary>
    public const int MaxTransactions = 240;

    /// <summary>
    /// NOTE ON SIGN. In a custom-user configuration Plaid documents `amount` as negative for a
    /// debit (money out) and positive for a credit. This is the OPPOSITE of the convention
    /// `/transactions/sync` uses when reading the data back, where positive means outflow —
    /// which is the one <c>PlaidEngine.MapToBudgetTrackerTransaction</c> inverts. Verify the
    /// round-trip against one real sync before trusting a seeded ledger.
    /// </summary>
    public static string Build(
        IReadOnlyList<SandboxSeedLine> expenses,
        decimal monthlyIncome,
        string incomeDescription,
        DateOnly from,
        DateOnly through,
        string seed = "budgettracker")
    {
        var transactions = new List<object>();

        // Walk whole months from `from` to `through`, emitting each month's income and outgoings.
        var month = new DateOnly(from.Year, from.Month, 1);
        var lastMonth = new DateOnly(through.Year, through.Month, 1);

        while (month <= lastMonth && transactions.Count < MaxTransactions)
        {
            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);

            // Income: paid twice monthly, on the 1st and the 15th.
            if (monthlyIncome > 0)
            {
                var half = Math.Round(monthlyIncome / 2m, 2, MidpointRounding.AwayFromZero);
                AddIfInRange(transactions, month, 1, half, incomeDescription, from, through, daysInMonth);
                AddIfInRange(transactions, month, 15, half, incomeDescription, from, through, daysInMonth);
            }

            foreach (var line in expenses)
            {
                var count = Math.Max(line.PerMonth, 1);
                var each = Math.Round(line.MonthlyAmount / count, 2, MidpointRounding.AwayFromZero);
                if (each <= 0) continue;

                for (var occurrence = 0; occurrence < count; occurrence++)
                {
                    // Spread occurrences evenly through the month, nudged off the 1st so rent and
                    // groceries do not all land on the same day.
                    var day = Math.Min(daysInMonth, 2 + (occurrence * daysInMonth / count));
                    AddIfInRange(transactions, month, day, -each, line.Description, from, through, daysInMonth);

                    if (transactions.Count >= MaxTransactions) break;
                }

                if (transactions.Count >= MaxTransactions) break;
            }

            month = month.AddMonths(1);
        }

        var config = new
        {
            seed,
            override_accounts = new[]
            {
                new
                {
                    type = "depository",
                    subtype = "checking",
                    starting_balance = 4200,
                    transactions = transactions.ToArray()
                }
            }
        };

        return JsonSerializer.Serialize(config);
    }

    private static void AddIfInRange(
        List<object> sink,
        DateOnly month,
        int day,
        decimal amount,
        string description,
        DateOnly from,
        DateOnly through,
        int daysInMonth)
    {
        var date = new DateOnly(month.Year, month.Month, Math.Clamp(day, 1, daysInMonth));
        if (date < from || date > through) return;

        var iso = date.ToString("yyyy-MM-dd");
        sink.Add(new
        {
            date_transacted = iso,
            date_posted = iso,
            amount,
            description,
            currency = "USD"
        });
    }
}
