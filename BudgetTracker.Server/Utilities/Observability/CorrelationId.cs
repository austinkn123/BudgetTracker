using System.Diagnostics.CodeAnalysis;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>
/// The per-request correlation ID: read from the inbound <c>X-Correlation-ID</c> header when it is safe to reuse,
/// otherwise generated, then echoed on the response and attached to every log line of the request.
/// </summary>
public static class CorrelationId
{
    /// <summary>The request and response header that carries the correlation ID.</summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>The longest inbound value accepted; anything longer is replaced.</summary>
    public const int MaxLength = 64;

    private static readonly object ItemsKey = new();

    /// <summary>Whether <paramref name="value"/> is safe to log and echo: 1–64 ASCII letters, digits, '-', '_' or '.'.</summary>
    /// <param name="value">The candidate correlation ID, typically from a request header.</param>
    /// <returns><c>true</c> when the value can be reused as-is.</returns>
    public static bool IsValid([NotNullWhen(true)] string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= MaxLength && value.All(IsSafe);

    /// <summary>Generates a new correlation ID.</summary>
    /// <returns>A 32-character lowercase hex string.</returns>
    public static string New() => Guid.NewGuid().ToString("N");

    /// <summary>The correlation ID assigned to <paramref name="context"/>.</summary>
    /// <param name="context">The current request.</param>
    /// <returns>The correlation ID, or the server's trace identifier if the correlation middleware has not run.</returns>
    public static string Get(HttpContext context) => context.Items[ItemsKey] as string ?? context.TraceIdentifier;

    internal static void Set(HttpContext context, string correlationId) => context.Items[ItemsKey] = correlationId;

    private static bool IsSafe(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.';
}
