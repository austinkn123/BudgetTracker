using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
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
    IDataVersionAccessor dataVersion,
    ISandboxSeedEngine seedEngine,
    IBudgetPlanEngine budgetPlanEngine,
    IOptions<PlaidOptions> options,
    ILogger<SandboxSeedManager> logger) : ISandboxSeedManager
{
    private readonly PlaidOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<Result<PlaidSyncSummary>> SeedAsync(int userId, int months, CancellationToken cancellationToken = default)
    {
        // Hard guard: this fabricates data and must never be reachable against real credentials.
        if (!string.Equals(_options.Environment, "sandbox", StringComparison.OrdinalIgnoreCase))
            return Result<PlaidSyncSummary>.Failure("Sandbox seeding is only available when Plaid is configured for sandbox.");

        if (seedEngine.ValidateMonths(months) is string monthsError)
            return Result<PlaidSyncSummary>.Failure(monthsError);

        var now = DateTime.UtcNow;
        var currentMonth = new DateTime(now.Year, now.Month, 1);

        // Same selection rule as the dashboard: the newest plan that has taken effect.
        var plans = await budgetPlanAccessor.GetByUserIdAsync(userId);
        var plan = budgetPlanEngine.SelectEffectivePlan(plans, currentMonth);

        if (plan is null)
            return Result<PlaidSyncSummary>.Failure("Create an active budget plan first — the seed is derived from it.");

        var categoryNames = (await categoryAccessor.GetByUserIdAsync(userId))
            .ToDictionary(c => c.Id, c => c.Name);

        var lines = seedEngine.BuildLines(plan, categoryNames);

        if (lines.Count == 0)
            return Result<PlaidSyncSummary>.Failure("The active plan has no expense entries to derive transactions from.");

        var through = DateOnly.FromDateTime(now);
        var from = DateOnly.FromDateTime(currentMonth.AddMonths(-(months - 1)));

        var config = seedEngine.BuildConfig(lines, plan.NetIncomeMonthly, from, through);

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
        var previousItem = seedEngine.SelectItemToReplace(activeBefore);

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
        // dedupe and persistence code a genuine connection would. It bumps the data version for what it writes.
        // TODO: SandboxSeedManager violates IDesign layering — consult tony.
        if (previousItem is null)
            return await plaidManager.ExchangePublicTokenAsync(userId, publicToken);

        try
        {
            var seeded = await plaidManager.ExchangePublicTokenAsync(userId, publicToken);

            // A failed initial sync still leaves the new item persisted, so success alone does not decide this.
            if (seeded.IsSuccess || await NewItemWasPersistedAsync(userId, activeBefore))
                await RevokePreviousItemAsync(previousItem.Id, previousToken!, cancellationToken);
            else
                await RestorePreviousItemAsync(userId, previousItem.Id);

            return seeded;
        }
        finally
        {
            // The previous item was taken offline above, so the user's connections changed whatever happens next.
            await dataVersion.BumpAsync(userId);
        }
    }

    /// <inheritdoc />
    public async Task<Result> ResetLoginAsync(int userId, int plaidItemId, CancellationToken cancellationToken = default)
    {
        // Same hard guard as SeedAsync: forcing a login failure only makes sense against sandbox items.
        if (!string.Equals(_options.Environment, "sandbox", StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Sandbox login reset is only available when Plaid is configured for sandbox.");

        var lookup = await itemAccessor.GetActiveAccessTokenAsync(userId, plaidItemId);
        if (lookup.Status == AccessTokenStatus.NotFound)
            return Result.Failure("Bank connection not found");
        if (lookup.Status == AccessTokenStatus.Undecryptable)
            return Result.Failure("A bank connection needs to be reconnected. Disconnect it and link it again.");

        try
        {
            // The NeedsReconnect flag is not set here: the next sync hits ITEM_LOGIN_REQUIRED and sets it,
            // which exercises the real detection path.
            await plaidAccessor.ResetSandboxLoginAsync(lookup.AccessToken!, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sandbox reset_login for PlaidItem {PlaidItemId} failed", plaidItemId);
            return Result.Failure("Could not reset the sandbox login. Check the Plaid sandbox credentials.");
        }
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
}
