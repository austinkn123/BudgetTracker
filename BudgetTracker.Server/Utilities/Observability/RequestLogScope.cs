using System.Collections;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>
/// The log scope opened for each request. It is mutable so that <see cref="UserId"/>, known only after authentication,
/// also appears on log lines written by middleware that sits outside authentication (e.g. the exception handler).
/// Holds identifiers only — never the Cognito sub, email, tokens or financial data.
/// </summary>
public sealed class RequestLogScope : IReadOnlyList<KeyValuePair<string, object?>>
{
    /// <summary>Scope key for the request's correlation ID.</summary>
    public const string CorrelationIdKey = "CorrelationId";

    /// <summary>Scope key for the Lambda invocation's request ID.</summary>
    public const string AwsRequestIdKey = "AwsRequestId";

    /// <summary>Scope key for the internal BudgetTracker user ID.</summary>
    public const string UserIdKey = "UserId";

    private static readonly object ItemsKey = new();

    /// <summary>Creates the scope for one request.</summary>
    /// <param name="correlationId">The request's correlation ID.</param>
    /// <param name="awsRequestId">The Lambda request ID, or <c>null</c> when not running in Lambda.</param>
    public RequestLogScope(string correlationId, string? awsRequestId)
    {
        CorrelationId = correlationId;
        AwsRequestId = awsRequestId;
    }

    /// <summary>The request's correlation ID.</summary>
    public string CorrelationId { get; }

    /// <summary>The Lambda request ID, or <c>null</c> when not running in Lambda.</summary>
    public string? AwsRequestId { get; }

    /// <summary>The internal user ID, once the request is authenticated and the user resolved.</summary>
    public int? UserId { get; internal set; }

    /// <inheritdoc />
    public int Count => Entries().Count;

    /// <inheritdoc />
    public KeyValuePair<string, object?> this[int index] => Entries()[index];

    /// <summary>The scope attached to <paramref name="context"/>, if the correlation middleware has run.</summary>
    /// <param name="context">The current request.</param>
    /// <returns>The request's scope, or <c>null</c>.</returns>
    public static RequestLogScope? For(HttpContext context) => context.Items[ItemsKey] as RequestLogScope;

    internal void AttachTo(HttpContext context) => context.Items[ItemsKey] = this;

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => Entries().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public override string ToString() => string.Join(' ', Entries().Select(e => $"{e.Key}:{e.Value}"));

    private List<KeyValuePair<string, object?>> Entries()
    {
        var entries = new List<KeyValuePair<string, object?>>(3) { new(CorrelationIdKey, CorrelationId) };
        if (AwsRequestId is not null)
            entries.Add(new(AwsRequestIdKey, AwsRequestId));
        if (UserId is not null)
            entries.Add(new(UserIdKey, UserId));
        return entries;
    }
}
