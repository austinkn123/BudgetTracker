namespace BudgetTracker.Domain.Plaid;

/// <summary>Outcome of reading a stored Plaid access_token.</summary>
public enum AccessTokenStatus
{
    /// <summary>The token was found and decrypted.</summary>
    Found,

    /// <summary>No matching active item exists.</summary>
    NotFound,

    /// <summary>
    /// The item exists but its token cannot be decrypted, for example because the Data Protection key ring was lost.
    /// The connection must be re-linked.
    /// </summary>
    Undecryptable
}

/// <summary>
/// Result of an access_token lookup. Lets callers tell a missing item apart from one whose token is unreadable,
/// without the accessor throwing.
/// </summary>
/// <param name="Status">Lookup outcome.</param>
/// <param name="AccessToken">The decrypted token when <paramref name="Status"/> is <see cref="AccessTokenStatus.Found"/>; otherwise null.</param>
public sealed record AccessTokenLookup(AccessTokenStatus Status, string? AccessToken)
{
    /// <summary>A lookup that found and decrypted the token.</summary>
    public static AccessTokenLookup Found(string accessToken) => new(AccessTokenStatus.Found, accessToken);

    /// <summary>A lookup that matched no active item.</summary>
    public static AccessTokenLookup NotFound { get; } = new(AccessTokenStatus.NotFound, null);

    /// <summary>A lookup that matched an item whose token could not be decrypted.</summary>
    public static AccessTokenLookup Undecryptable { get; } = new(AccessTokenStatus.Undecryptable, null);
}
