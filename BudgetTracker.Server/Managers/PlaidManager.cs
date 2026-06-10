using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Server.Managers;

/// <summary>
/// Orchestrates Plaid Link, token exchange, transaction sync, and replace/disconnect flows.
/// Owns the Fetch → Compute → Persist sequencing; never decodes or stores raw access_tokens itself
/// (encryption lives in <see cref="IPlaidItemAccessor"/>).
/// </summary>
public class PlaidManager(
    IPlaidAccessor plaidAccessor,
    IPlaidItemAccessor itemAccessor,
    ITransactionAccessor transactionAccessor,
    IAccountAccessor accountAccessor,
    IPlaidEngine engine) : IPlaidManager
{
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

        // Persist — atomic replace + snapshot of Plaid accounts
        int newPlaidItemId;
        try
        {
            newPlaidItemId = await itemAccessor.ReplaceActiveAsync(
                userId,
                accessToken,
                plaidItemId,
                metadata.InstitutionId,
                metadata.InstitutionName,
                metadata.ConsentExpiresAt,
                accountSnapshots);
        }
        catch (Exception)
        {
            return Result<PlaidSyncSummary>.Failure("Could not save the new bank connection. Please try again.");
        }

        // Initial sync (~30 days delivered automatically by Plaid via /transactions/sync with null cursor)
        return await RunSyncAsync(userId, newPlaidItemId, accessToken, cursor: null, accountsLookup: allAccounts, plaidAccountSnapshots: accountSnapshots);
    }

    /// <inheritdoc />
    public async Task<Result<PlaidSyncSummary>> SyncAsync(int userId)
    {
        var plaidItem = await itemAccessor.GetActiveByUserIdAsync(userId);
        if (plaidItem is null)
            return Result<PlaidSyncSummary>.Failure("No active bank connection to sync");

        var accessToken = await itemAccessor.GetActiveAccessTokenAsync(userId);
        if (accessToken is null)
            return Result<PlaidSyncSummary>.Failure("No active bank connection to sync");

        var userAccounts = await accountAccessor.GetByUserIdAsync(userId);
        return await RunSyncAsync(userId, plaidItem.Id, accessToken, plaidItem.SyncCursor, userAccounts, plaidItem.Accounts.ToList());
    }

    /// <inheritdoc />
    public async Task<Result<PlaidConnectionView>> GetConnectionAsync(int userId)
    {
        var plaidItem = await itemAccessor.GetActiveByUserIdAsync(userId);
        if (plaidItem is null)
            return Result<PlaidConnectionView>.Failure("No active bank connection");

        var view = new PlaidConnectionView(
            plaidItem.Id,
            plaidItem.InstitutionName,
            plaidItem.LastSyncedAt,
            plaidItem.Accounts.Select(a => new PlaidLinkedAccountView(a.Name, a.Mask, a.AccountType)).ToList());

        return Result<PlaidConnectionView>.Success(view);
    }

    /// <inheritdoc />
    public async Task<Result> DisconnectAsync(int userId)
    {
        var accessToken = await itemAccessor.GetActiveAccessTokenAsync(userId);
        if (accessToken is null)
            return Result.Failure("No active bank connection to disconnect");

        try
        {
            await plaidAccessor.RemoveItemAsync(accessToken);
        }
        catch
        {
            // Best-effort: deactivate locally even if Plaid is unreachable.
        }

        await itemAccessor.DeactivateActiveAsync(userId);
        return Result.Success();
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

            mapped.Add(engine.MapToBudgetTrackerTransaction(plaidTxn, resolvedAccountId.Value));
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
