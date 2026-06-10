namespace BudgetTracker.Domain.Plaid;

/// <summary>Result of <c>/link/token/create</c>.</summary>
public record PlaidLinkTokenResult(string LinkToken, DateTime Expiration);

/// <summary>Result of <c>/item/public_token/exchange</c>.</summary>
public record PlaidExchangeResult(string AccessToken, string ItemId);

/// <summary>Subset of fields we persist about a Plaid account returned by <c>/accounts/get</c> or <c>/transactions/sync</c>.</summary>
public record PlaidAccountDto(
    string AccountId,
    string Name,
    string? Mask,
    string Type,
    string? Subtype);

/// <summary>Plaid item metadata returned by <c>/item/get</c>.</summary>
public record PlaidItemMetadata(
    string ItemId,
    string InstitutionId,
    string InstitutionName,
    DateTime? ConsentExpiresAt);

/// <summary>One Plaid transaction as returned by <c>/transactions/sync</c> (raw — sign-as-Plaid-reports-it).</summary>
public record PlaidTransactionDto(
    string TransactionId,
    string AccountId,
    decimal Amount,
    DateTime Date,
    string? MerchantName,
    string Name,
    bool Pending,
    string? PersonalFinanceCategoryPrimary);

/// <summary>Aggregated payload from a full /transactions/sync paginated walk.</summary>
public record PlaidSyncResult(
    IReadOnlyList<PlaidTransactionDto> Added,
    IReadOnlyList<PlaidTransactionDto> Modified,
    IReadOnlyList<string> RemovedTransactionIds,
    string NextCursor);
