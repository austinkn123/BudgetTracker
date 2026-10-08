using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Server.Managers;

/// <inheritdoc />
public class SandboxSeedManager(
    IPlaidAccessor plaidAccessor,
    IPlaidManager plaidManager,
    IPlaidItemAccessor itemAccessor,
    IBudgetPlanAccessor budgetPlanAccessor,
    ICategoryAccessor categoryAccessor,
    IOptions<PlaidOptions> options,
    ILogger<SandboxSeedManager> logger) : ISandboxSeedManager
{
    private readonly PlaidOptions _options = options.Value;

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
    public async Task<Result<PlaidSyncSummary>> SeedAsync(int userId, int months, CancellationToken cancellationToken = default)
    {
        // Hard guard: this fabricates data and must never be reachable against real credentials.
        if (!string.Equals(_options.Environment, "sandbox", StringComparison.OrdinalIgnoreCase))
            return Result<PlaidSyncSummary>.Failure("Sandbox seeding is only available when Plaid is configured for sandbox.");

        if (months is < 1 or > 12)
            return Result<PlaidSyncSummary>.Failure("Months must be between 1 and 12");

        var now = DateTime.UtcNow;
        var currentMonth = new DateTime(now.Year, now.Month, 1);

        // Same selection rule as the dashboard: the newest plan that has taken effect.
        var plans = await budgetPlanAccessor.GetByUserIdAsync(userId);
        var plan = plans
            .Where(p => p.IsActive && p.PlanMonth <= currentMonth)
            .OrderByDescending(p => p.PlanMonth)
            .FirstOrDefault();

        if (plan is null)
            return Result<PlaidSyncSummary>.Failure("Create an active budget plan first — the seed is derived from it.");

        var categoryNames = (await categoryAccessor.GetByUserIdAsync(userId))
            .ToDictionary(c => c.Id, c => c.Name);

        var lines = plan.Entries
            .Where(e => e.LineType == "Expense" && e.MonthlyEquivalent > 0)
            .Select(e => BuildLine(e.CategoryId is int id && categoryNames.TryGetValue(id, out var name) ? name : "General", e.MonthlyEquivalent))
            .ToList();

        if (lines.Count == 0)
            return Result<PlaidSyncSummary>.Failure("The active plan has no expense entries to derive transactions from.");

        var through = DateOnly.FromDateTime(now);
        var from = DateOnly.FromDateTime(currentMonth.AddMonths(-(months - 1)));

        var config = SandboxSeedBuilder.Build(
            lines,
            plan.NetIncomeMonthly,
            "Employer Direct Deposit",
            from,
            through);

        string publicToken;
        try
        {
            publicToken = await plaidAccessor.CreateSandboxPublicTokenAsync(config, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sandbox seed: creating the custom public_token failed");
            return Result<PlaidSyncSummary>.Failure("Could not create the sandbox item. Check the Plaid sandbox credentials.");
        }

        // Captured before the exchange so the newly seeded item is never the one replaced. Users may link many
        // banks, so only the most recent one (the previous seed or stock sandbox link) is retired.
        var activeBefore = await itemAccessor.GetAllActiveByUserIdAsync(userId);
        var previousItem = activeBefore
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .FirstOrDefault();

        // Every seed shares an institution and override_accounts, so while the previous seed is active the
        // exchange's IsAlreadyLinked check rejects the new one. Take it out of the active set first; its Plaid
        // Item stays alive until the new one is persisted, so a failed exchange can simply restore it.
        AccessTokenLookup? previousToken = null;
        if (previousItem is not null)
        {
            previousToken = await itemAccessor.GetActiveAccessTokenAsync(userId, previousItem.Id);
            await itemAccessor.DeactivateAsync(userId, previousItem.Id);
        }

        // Deliberately reuses the real link path, so seeded data exercises the same mapping,
        // dedupe and persistence code a genuine connection would.
        // TODO: SandboxSeedManager violates IDesign layering — consult tony.
        var seeded = await plaidManager.ExchangePublicTokenAsync(userId, publicToken);
        if (previousItem is null)
            return seeded;

        // A failed initial sync still leaves the new item persisted, so success alone does not decide this.
        if (seeded.IsSuccess || await NewItemWasPersistedAsync(userId, activeBefore))
            await RevokePreviousItemAsync(previousItem.Id, previousToken!, cancellationToken);
        else
            await RestorePreviousItemAsync(userId, previousItem.Id);

        return seeded;
    }

    private async Task<bool> NewItemWasPersistedAsync(int userId, IReadOnlyList<PlaidItem> activeBefore)
    {
        var knownIds = activeBefore.Select(i => i.Id).ToHashSet();
        return (await itemAccessor.GetAllActiveByUserIdAsync(userId)).Any(i => !knownIds.Contains(i.Id));
    }

    /// <summary>Best-effort <c>/item/remove</c> for the already-deactivated previous item.</summary>
    private async Task RevokePreviousItemAsync(int plaidItemId, AccessTokenLookup lookup, CancellationToken cancellationToken)
    {
        if (lookup.Status == AccessTokenStatus.Undecryptable)
        {
            logger.LogWarning(
                "Sandbox seed: access token for PlaidItem {PlaidItemId} cannot be decrypted; skipping Plaid /item/remove and deactivating locally.",
                plaidItemId);
        }

        if (lookup.AccessToken is not { } accessToken)
            return;

        try
        {
            await plaidAccessor.RemoveItemAsync(accessToken, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sandbox seed: revoking PlaidItem {PlaidItemId} failed; deactivating locally.", plaidItemId);
        }
    }

    /// <summary>
    /// Undoes the pre-exchange deactivation. If that also fails the previous item is merely left inactive: its Plaid
    /// Item was never removed and its rows are intact, so the user can seed or link again.
    /// </summary>
    private async Task RestorePreviousItemAsync(int userId, int plaidItemId)
    {
        try
        {
            await itemAccessor.ReactivateAsync(userId, plaidItemId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sandbox seed: restoring PlaidItem {PlaidItemId} after a failed exchange failed; it is left inactive.", plaidItemId);
        }
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
