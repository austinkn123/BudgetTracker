using System.Net;

namespace BudgetTracker.Domain.Webull;

/// <summary>
/// A failed Webull call, already classified. Every failure leaves the Webull accessor as this type (network errors,
/// timeouts and unreadable responses included), so callers catch exactly one type. The message is sanitized: HTTP status
/// and Webull's <c>error_code</c> only, never headers, the signature, the App Key or Secret, or the access token.
/// </summary>
/// <param name="kind">How the caller should react.</param>
/// <param name="errorCode">Webull's <c>error_code</c> (e.g. <c>UNAUTHORIZED</c>, <c>INVALID_TOKEN</c>), or null when there was none.</param>
/// <param name="message">The sanitized message.</param>
/// <param name="statusCode">The HTTP status, or null when no response arrived.</param>
/// <param name="innerException">The underlying transport exception, if any.</param>
public class WebullApiException(
    WebullErrorKind kind,
    string? errorCode,
    string message,
    HttpStatusCode? statusCode,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>How the caller should react.</summary>
    public WebullErrorKind Kind { get; } = kind;

    /// <summary>Webull's <c>error_code</c>, or null when the response had none.</summary>
    public string? ErrorCode { get; } = errorCode;

    /// <summary>The HTTP status, or null when no response arrived.</summary>
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
