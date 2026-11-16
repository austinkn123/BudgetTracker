namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>Source-generated log messages for the Plaid link, sync and sandbox workflows. Event IDs 2000–2999.</summary>
public static partial class PlaidLog
{
    /// <summary>Creating the sandbox item's custom public_token failed.</summary>
    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Sandbox seed: creating the custom public_token failed")]
    public static partial void SandboxSeedTokenFailed(this ILogger logger, Exception exception);

    /// <summary>Plaid's sandbox reset_login call failed.</summary>
    [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Sandbox reset_login for PlaidItem {PlaidItemId} failed")]
    public static partial void SandboxResetLoginFailed(this ILogger logger, Exception exception, int plaidItemId);

    /// <summary>A newly exchanged item could not be saved, so it is revoked at Plaid.</summary>
    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Persisting new Plaid item {PlaidItemId} failed; revoking it at Plaid.")]
    public static partial void PersistNewItemFailed(this ILogger logger, Exception exception, string plaidItemId);

    /// <summary>A user-triggered sync of one item failed; the other items still sync.</summary>
    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "On-demand sync failed for PlaidItem {PlaidItemId}; continuing.")]
    public static partial void OnDemandSyncFailed(this ILogger logger, Exception exception, int plaidItemId);

    /// <summary>Disconnect: the stored access token cannot be decrypted, so the item is only deactivated locally.</summary>
    [LoggerMessage(EventId = 2005, Level = LogLevel.Warning, Message = "Access token for PlaidItem {PlaidItemId} cannot be decrypted; skipping Plaid /item/remove and deactivating locally.")]
    public static partial void DisconnectTokenUndecryptable(this ILogger logger, int plaidItemId);

    /// <summary>Disconnect: Plaid's /item/remove failed, so the item is only deactivated locally.</summary>
    [LoggerMessage(EventId = 2006, Level = LogLevel.Warning, Message = "Plaid /item/remove failed for PlaidItem {PlaidItemId}; deactivating locally.")]
    public static partial void ItemRemoveFailed(this ILogger logger, Exception exception, int plaidItemId);

    /// <summary>Registering the webhook failed; the sync goes ahead.</summary>
    [LoggerMessage(EventId = 2007, Level = LogLevel.Warning, Message = "Webhook registration failed for PlaidItem {PlaidItemId}; syncing anyway.")]
    public static partial void WebhookRegistrationFailed(this ILogger logger, Exception exception, int plaidItemId);

    /// <summary>The background sweep failed for one item; the other items still sync.</summary>
    [LoggerMessage(EventId = 2008, Level = LogLevel.Warning, Message = "Sweep sync failed for PlaidItem {PlaidItemId}; continuing.")]
    public static partial void SweepSyncFailed(this ILogger logger, Exception exception, int plaidItemId);

    /// <summary>Sync: the stored access token cannot be decrypted, so the item is skipped until re-linked.</summary>
    [LoggerMessage(EventId = 2009, Level = LogLevel.Warning, Message = "Access token for PlaidItem {PlaidItemId} cannot be decrypted; skipping sync until it is re-linked.")]
    public static partial void SyncTokenUndecryptable(this ILogger logger, int plaidItemId);

    /// <summary>Revoking an exchanged-but-unkept item failed; Plaid may still bill for it.</summary>
    [LoggerMessage(EventId = 2010, Level = LogLevel.Warning, Message = "Plaid /item/remove failed for unkept Plaid item {PlaidItemId}; it may still be billed.")]
    public static partial void UnkeptItemRemoveFailed(this ILogger logger, Exception exception, string plaidItemId);

    /// <summary>Sandbox seed: the previous item's token cannot be decrypted, so it is only deactivated locally.</summary>
    [LoggerMessage(EventId = 2011, Level = LogLevel.Warning, Message = "Sandbox seed: access token for PlaidItem {PlaidItemId} cannot be decrypted; skipping Plaid /item/remove and deactivating locally.")]
    public static partial void SandboxSeedTokenUndecryptable(this ILogger logger, int plaidItemId);

    /// <summary>Sandbox seed: revoking the previous item failed, so it is only deactivated locally.</summary>
    [LoggerMessage(EventId = 2012, Level = LogLevel.Warning, Message = "Sandbox seed: revoking PlaidItem {PlaidItemId} failed; deactivating locally.")]
    public static partial void SandboxSeedRevokeFailed(this ILogger logger, Exception exception, int plaidItemId);

    /// <summary>Sandbox seed: restoring the previous item after a failed exchange failed; it stays inactive.</summary>
    [LoggerMessage(EventId = 2013, Level = LogLevel.Error, Message = "Sandbox seed: restoring PlaidItem {PlaidItemId} after a failed exchange failed; it is left inactive.")]
    public static partial void SandboxSeedRestoreFailed(this ILogger logger, Exception exception, int plaidItemId);

    /// <summary>Plaid's /link/token/create failed.</summary>
    [LoggerMessage(EventId = 2014, Level = LogLevel.Warning, Message = "Plaid /link/token/create failed (update mode: {IsUpdateMode}).")]
    public static partial void LinkTokenCreateFailed(this ILogger logger, Exception exception, bool isUpdateMode);

    /// <summary>Pairing transfer legs failed; the next sync retries it.</summary>
    [LoggerMessage(EventId = 2015, Level = LogLevel.Warning, Message = "Transfer pairing failed for user {UserId}; it will retry on the next sync.")]
    public static partial void TransferPairingFailed(this ILogger logger, Exception exception, int userId);

    /// <summary>One tick of the background sweep service failed; the next interval retries.</summary>
    [LoggerMessage(EventId = 2016, Level = LogLevel.Error, Message = "Plaid sweep tick failed; will retry on the next interval.")]
    public static partial void SweepTickFailed(this ILogger logger, Exception exception);
}
