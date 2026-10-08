using System.Text.Json;
using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Server.Managers;

/// <summary>
/// Orchestrates Plaid Link, token exchange, transaction sync, and per-connection disconnect for users with many linked banks.
/// Owns the Fetch → Compute → Persist sequencing; never decodes or stores raw access_tokens itself
/// (encryption lives in <see cref="IPlaidItemAccessor"/>).
/// </summary>
public class PlaidManager(
    IPlaidAccessor plaidAccessor,
    IPlaidItemAccessor itemAccessor,
    ITransactionAccessor transactionAccessor,
    IAccountAccessor accountAccessor,
    ICategoryAccessor categoryAccessor,
    IPlaidEngine engine,
    IPlaidWebhookEngine webhookEngine,
    IOptions<PlaidOptions> options,
    ILogger<PlaidManager> logger) : IPlaidManager
{
    /// <summary>Transactions webhook codes that warrant a sync. Others (e.g. RECURRING_*) are ignored.</summary>
    private static readonly HashSet<string> ActionableTransactionCodes =
    [
        "SYNC_UPDATES_AVAILABLE",
        "DEFAULT_UPDATE",
        "HISTORICAL_UPDATE",
        "INITIAL_UPDATE"
    ];

    private const string AlreadyLinkedMessage = "This institution is already linked. Disconnect it first if you want to link it again.";
    private const string ReconnectMessage = "A bank connection needs to be reconnected. Disconnect it and link it again.";

    private readonly PlaidOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<Result<PlaidLinkTokenResult>> CreateLinkTokenAsync(int userId, string cognitoSub)
    {
        if (string.IsNullOrWhiteSpace(cognitoSub))
            return Result<PlaidLinkTokenResult>.Failure("Authenticated user identity is missing");

        try
        {
            var token = await plaidAccessor.CreateLinkTokenAsync(cognitoSub);
            return Result<PlaidLinkTokenResult>.Success(token);
        }
        catch (Exception)
        {
            // Plain-language error per AC-9 — never echo upstream message (may contain secret/access_token).
            return Result<PlaidLinkTokenResult>.Failure("Unable to start bank link right now. Please try again in a moment.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<PlaidSyncSummary>> ExchangePublicTokenAsync(int userId, string publicToken)
    {
        if (string.IsNullOrWhiteSpace(publicToken))
            return Result<PlaidSyncSummary>.Failure("Public token is required");

        string accessToken;
        string plaidItemId;
        PlaidItemMetadata metadata;
        IReadOnlyList<PlaidAccountDto> plaidAccounts;

        try
        {
            // Fetch — exchange + metadata + accounts
            var exchange = await plaidAccessor.ExchangePublicTokenAsync(publicToken);
            accessToken = exchange.AccessToken;
            plaidItemId = exchange.ItemId;
            metadata = await plaidAccessor.GetItemMetadataAsync(accessToken);
            plaidAccounts = await plaidAccessor.GetAccountsAsync(accessToken);
        }
        catch (Exception)
        {
            return Result<PlaidSyncSummary>.Failure("Could not link your bank. Please try again.");
        }

        // Fetch + Compute — refuse a re-link of an active institution before any row is written,
        // so the global unique index on PlaidAccountId is never what rejects it.
        var activeItems = await itemAccessor.GetAllActiveByUserIdAsync(userId);
        if (engine.IsAlreadyLinked(metadata.InstitutionId, plaidAccounts, activeItems))
        {
            await RevokeUnkeptItemAsync(accessToken, plaidItemId);
            return Result<PlaidSyncSummary>.Failure(AlreadyLinkedMessage);
        }

        // Compute — ensure a BudgetTracker account exists for each Plaid account
        var userAccounts = await accountAccessor.GetByUserIdAsync(userId);
        var allAccounts = userAccounts.ToList();
        var accountSnapshots = new List<PlaidAccount>();

        foreach (var plaidAcct in plaidAccounts)
        {
            var existingId = engine.ResolveBudgetTrackerAccountId(plaidAcct, allAccounts);
            if (existingId is null)
            {
                var newAccount = engine.BuildBudgetTrackerAccount(plaidAcct, metadata.InstitutionName, userId);
                var newId = await accountAccessor.CreateAsync(newAccount);
                newAccount.Id = newId;
                allAccounts.Add(newAccount);
            }

            accountSnapshots.Add(new PlaidAccount
            {
                PlaidAccountId = plaidAcct.AccountId,
                Mask = plaidAcct.Mask,
                Name = plaidAcct.Name,
                AccountType = plaidAcct.Type,
                AccountSubtype = plaidAcct.Subtype
            });
        }

        // Persist — add the new item (existing connections stay active) + snapshot of Plaid accounts
        int newPlaidItemId;
        try
        {
            newPlaidItemId = await itemAccessor.AddAsync(
                userId,
                accessToken,
                plaidItemId,
                metadata.InstitutionId,
                metadata.InstitutionName,
                metadata.ConsentExpiresAt,
                accountSnapshots);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Persisting new Plaid item {PlaidItemId} failed; revoking it at Plaid.", plaidItemId);
            await RevokeUnkeptItemAsync(accessToken, plaidItemId);
            return Result<PlaidSyncSummary>.Failure("Could not save the new bank connection. Please try again.");
        }

        // Initial sync (~30 days delivered automatically by Plaid via /transactions/sync with null cursor)
        return await RunSyncAsync(userId, newPlaidItemId, accessToken, cursor: null, accountsLookup: allAccounts, plaidAccountSnapshots: accountSnapshots);
    }

    /// <inheritdoc />
    public async Task<Result<PlaidSyncSummary>> SyncAsync(int userId, int? staleAfterHours = null)
    {
        // Fetch
        var items = await itemAccessor.GetAllActiveByUserIdAsync(userId);

        // Compute
        var now = DateTime.UtcNow;
        var due = engine.SelectItemsDueForSync(items, now, staleAfterHours);
        if (!due.IsSuccess)
            return Result<PlaidSyncSummary>.Failure(due.Error!);

        if (due.Value!.Count == 0)
            return Result<PlaidSyncSummary>.Success(new PlaidSyncSummary(0, 0, 0, now));

        // Persist — each item syncs independently so one broken bank never blocks the others.
        int inserted = 0, updated = 0, removed = 0;
        DateTime? syncedAt = null;
        string? firstError = null;

        foreach (var item in due.Value)
        {
            Result<PlaidSyncSummary> result;
            try
            {
                result = await SyncItemAsync(item);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "On-demand sync failed for PlaidItem {PlaidItemId}; continuing.", item.Id);
                result = Result<PlaidSyncSummary>.Failure("Could not refresh transactions right now. Please try again.");
            }

            if (!result.IsSuccess)
            {
                firstError ??= result.Error;
                continue;
            }

            inserted += result.Value!.Inserted;
            updated += result.Value.Updated;
            removed += result.Value.Removed;
            syncedAt = result.Value.SyncedAt;
        }

        return syncedAt is DateTime completedAt
            ? Result<PlaidSyncSummary>.Success(new PlaidSyncSummary(inserted, updated, removed, completedAt))
            : Result<PlaidSyncSummary>.Failure(firstError!);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlaidConnectionView>> GetConnectionsAsync(int userId)
    {
        var items = await itemAccessor.GetAllActiveByUserIdAsync(userId);

        return items
            .Select(item => new PlaidConnectionView(
                item.Id,
                item.InstitutionName,
                item.LastSyncedAt,
                item.Accounts.Select(a => new PlaidLinkedAccountView(a.PlaidAccountId, a.Name, a.Mask, a.AccountType)).ToList()))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Result> DisconnectAsync(int userId, int plaidItemId)
    {
        var lookup = await itemAccessor.GetActiveAccessTokenAsync(userId, plaidItemId);
        switch (lookup.Status)
        {
            case AccessTokenStatus.NotFound:
                return Result.Failure("Bank connection not found");

            case AccessTokenStatus.Undecryptable:
                // Without the token Plaid cannot be told to stop billing; the user still needs to be able to unlink.
                logger.LogWarning(
                    "Access token for PlaidItem {PlaidItemId} cannot be decrypted; skipping Plaid /item/remove and deactivating locally.",
                    plaidItemId);
                break;

            case AccessTokenStatus.Found:
                try
                {
                    await plaidAccessor.RemoveItemAsync(lookup.AccessToken!);
                }
                catch (Exception ex)
                {
                    // Best-effort: deactivate locally even if Plaid is unreachable.
                    logger.LogWarning(ex, "Plaid /item/remove failed for PlaidItem {PlaidItemId}; deactivating locally.", plaidItemId);
                }
                break;
        }

        return await itemAccessor.DeactivateAsync(userId, plaidItemId)
            ? Result.Success()
            : Result.Failure("Bank connection not found");
    }

    /// <inheritdoc />
    public async Task<Result> HandleTransactionsWebhookAsync(string plaidVerificationJwt, string rawBody)
    {
        // Verify — extract kid, fetch the matching JWK, verify signature + freshness + body hash.
        // ANY failure is a silent no-op success so the endpoint returns a neutral 200 that leaks nothing.
        var keyId = webhookEngine.ExtractKeyId(plaidVerificationJwt);
        if (keyId is null)
            return Result.Success();

        PlaidJwkDto jwk;
        try
        {
            jwk = await plaidAccessor.GetWebhookVerificationKeyAsync(keyId);
        }
        catch (Exception)
        {
            return Result.Success();
        }

        if (!webhookEngine.VerifyWebhook(plaidVerificationJwt, jwk, rawBody))
            return Result.Success();

        // Parse — act only on actionable TRANSACTIONS events; everything else is a no-op success.
        if (!TryParseTransactionsWebhook(rawBody, out var itemId))
            return Result.Success();

        var item = await itemAccessor.GetByPlaidItemIdAsync(itemId);
        if (item is null)
            return Result.Success();

        // An undecryptable token will not heal on a Plaid retry, so it is acknowledged rather than failed.
        var lookup = await itemAccessor.GetAccessTokenByPlaidItemIdAsync(item.PlaidItemId);
        if (lookup.Status == AccessTokenStatus.Undecryptable)
            LogUndecryptableSkip(item.Id);
        if (lookup.AccessToken is not { } accessToken)
            return Result.Success();

        var sync = await SyncItemWithTokenAsync(item, accessToken);
        return sync.IsSuccess ? Result.Success() : Result.Failure(sync.Error!);
    }

    /// <inheritdoc />
    public async Task<Result> SweepAllAsync()
    {
        var items = await itemAccessor.GetAllActiveAsync();
        var registerWebhook = !string.IsNullOrWhiteSpace(_options.WebhookUrl);

        foreach (var item in items)
        {
            try
            {
                var lookup = await itemAccessor.GetAccessTokenByPlaidItemIdAsync(item.PlaidItemId);
                if (lookup.Status == AccessTokenStatus.Undecryptable)
                    LogUndecryptableSkip(item.Id);
                if (lookup.AccessToken is not { } accessToken)
                    continue;

                // Register the webhook on pre-existing links (those created before WebhookUrl was set).
                // Best-effort in its OWN try/catch: a transient /item/webhook/update failure must never
                // skip this item's actual sync, which is the sweep's primary job.
                if (registerWebhook)
                {
                    try
                    {
                        await plaidAccessor.UpdateWebhookAsync(accessToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Webhook registration failed for PlaidItem {PlaidItemId}; syncing anyway.", item.Id);
                    }
                }

                var userAccounts = await accountAccessor.GetByUserIdAsync(item.UserId);
                await RunSyncAsync(item.UserId, item.Id, accessToken, item.SyncCursor, userAccounts, item.Accounts.ToList());
            }
            catch (Exception ex)
            {
                // One bad item must not abort the sweep. Never log the token/body.
                logger.LogWarning(ex, "Sweep sync failed for PlaidItem {PlaidItemId}; continuing.", item.Id);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Shared per-item sync: load the decrypted token (by Plaid item_id) and the user's accounts,
    /// then delegate to <see cref="RunSyncAsync"/>. Used by on-demand sync.
    /// </summary>
    private async Task<Result<PlaidSyncSummary>> SyncItemAsync(PlaidItem item)
    {
        var lookup = await itemAccessor.GetAccessTokenByPlaidItemIdAsync(item.PlaidItemId);
        if (lookup.Status == AccessTokenStatus.Undecryptable)
        {
            LogUndecryptableSkip(item.Id);
            return Result<PlaidSyncSummary>.Failure(ReconnectMessage);
        }

        if (lookup.AccessToken is not { } accessToken)
            return Result<PlaidSyncSummary>.Failure("No active bank connection to sync");

        return await SyncItemWithTokenAsync(item, accessToken);
    }

    private async Task<Result<PlaidSyncSummary>> SyncItemWithTokenAsync(PlaidItem item, string accessToken)
    {
        var userAccounts = await accountAccessor.GetByUserIdAsync(item.UserId);
        return await RunSyncAsync(item.UserId, item.Id, accessToken, item.SyncCursor, userAccounts, item.Accounts.ToList());
    }

    private void LogUndecryptableSkip(int plaidItemId) =>
        logger.LogWarning("Access token for PlaidItem {PlaidItemId} cannot be decrypted; skipping sync until it is re-linked.", plaidItemId);

    /// <summary>
    /// Best-effort <c>/item/remove</c> for an item that was exchanged but will not be kept. The exchange already
    /// created a billable Item at Plaid, so it must be revoked; a failure is logged and never surfaced to the user.
    /// </summary>
    private async Task RevokeUnkeptItemAsync(string accessToken, string plaidItemId)
    {
        try
        {
            await plaidAccessor.RemoveItemAsync(accessToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Plaid /item/remove failed for unkept Plaid item {PlaidItemId}; it may still be billed.", plaidItemId);
        }
    }

    /// <summary>
    /// Parse a webhook body, returning true (with the Plaid <paramref name="itemId"/>) only for an
    /// actionable <c>TRANSACTIONS</c> event. Malformed JSON or a non-actionable event yields false.
    /// </summary>
    private static bool TryParseTransactionsWebhook(string rawBody, out string itemId)
    {
        itemId = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("webhook_type", out var typeProp) || typeProp.GetString() != "TRANSACTIONS")
                return false;

            if (!root.TryGetProperty("webhook_code", out var codeProp) ||
                codeProp.GetString() is not { } code ||
                !ActionableTransactionCodes.Contains(code))
                return false;

            if (!root.TryGetProperty("item_id", out var itemProp) || itemProp.GetString() is not { } id)
                return false;

            itemId = id;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Shared sync routine: pull deltas, map via the engine, upsert by Plaid id, delete removed,
    /// then persist the next cursor. Used for both the initial post-exchange sync and the on-demand Refresh.
    /// </summary>
    private async Task<Result<PlaidSyncSummary>> RunSyncAsync(
        int userId,
        int plaidItemId,
        string accessToken,
        string? cursor,
        IReadOnlyList<Account> accountsLookup,
        IReadOnlyList<PlaidAccount> plaidAccountSnapshots)
    {
        PlaidSyncResult syncResult;
        try
        {
            syncResult = await plaidAccessor.SyncTransactionsAsync(accessToken, cursor);
        }
        catch (Exception)
        {
            return Result<PlaidSyncSummary>.Failure("Could not refresh transactions right now. Please try again.");
        }

        var changes = syncResult.Added.Concat(syncResult.Modified).ToList();
        var mapped = new List<Transaction>(changes.Count);

        // Plaid sends a suggested category on every transaction; resolving it here is what lets
        // imported rows reach BudgetAnalysisEngine, which groups purely by CategoryId (BUD-9).
        var userCategories = (await categoryAccessor.GetByUserIdAsync(userId)).ToList();

        foreach (var plaidTxn in changes)
        {
            var matchingSnapshot = plaidAccountSnapshots.FirstOrDefault(a => a.PlaidAccountId == plaidTxn.AccountId);
            if (matchingSnapshot is null)
                continue;

            var plaidAcctDto = new PlaidAccountDto(
                matchingSnapshot.PlaidAccountId,
                matchingSnapshot.Name,
                matchingSnapshot.Mask,
                matchingSnapshot.AccountType,
                matchingSnapshot.AccountSubtype);

            var resolvedAccountId = engine.ResolveBudgetTrackerAccountId(plaidAcctDto, accountsLookup);
            if (resolvedAccountId is null)
                continue; // Defensive — exchange flow ensures one exists; skip orphans rather than crash.

            var resolvedCategoryId = engine.ResolveCategoryId(plaidTxn, userCategories);
            mapped.Add(engine.MapToBudgetTrackerTransaction(plaidTxn, resolvedAccountId.Value, resolvedCategoryId));
        }

        var (inserted, updated) = await transactionAccessor.UpsertImportedAsync(mapped);
        var removed = 0;
        if (syncResult.RemovedTransactionIds.Count > 0)
        {
            removed = await transactionAccessor.DeleteByPlaidTransactionIdsAsync(syncResult.RemovedTransactionIds);
        }

        var syncedAt = DateTime.UtcNow;
        await itemAccessor.UpdateSyncStateAsync(plaidItemId, syncResult.NextCursor, syncedAt);

        return Result<PlaidSyncSummary>.Success(new PlaidSyncSummary(inserted, updated, removed, syncedAt));
    }
}
