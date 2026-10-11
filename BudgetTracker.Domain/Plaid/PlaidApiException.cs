using System.Net;

namespace BudgetTracker.Domain.Plaid;

/// <summary>
/// A non-2xx Plaid response. Subclasses <see cref="HttpRequestException"/> so existing catches keep working, and carries
/// Plaid's <c>error_code</c> so callers can react to specific failures such as <c>ITEM_LOGIN_REQUIRED</c>.
/// The message is sanitized — it never includes the request body (client_id/secret/access_token).
/// </summary>
/// <param name="errorCode">Plaid's <c>error_code</c>, or null when the response body had none.</param>
/// <param name="message">The sanitized message.</param>
/// <param name="statusCode">The HTTP status Plaid returned.</param>
public class PlaidApiException(string? errorCode, string message, HttpStatusCode statusCode)
    : HttpRequestException(message, null, statusCode)
{
    /// <summary>Plaid's <c>error_code</c> (e.g. <c>ITEM_LOGIN_REQUIRED</c>), or null when the response body had none.</summary>
    public string? ErrorCode { get; } = errorCode;
}
