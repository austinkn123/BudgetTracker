using System.Text.RegularExpressions;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>
/// Reads the AWS X-Ray trace ID Lambda assigns to an invocation, so a log line can be matched to its trace. Tracing
/// itself is done by the Lambda service (<c>Tracing: Active</c> in deploy/template.yaml); the app adds no X-Ray SDK,
/// which is in maintenance mode and ends support on 2027-02-25.
/// </summary>
public static partial class XRayTrace
{
    private const string RootPrefix = "Root=";

    /// <summary>
    /// The root trace ID from a Lambda trace header such as
    /// <c>Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1</c>.
    /// </summary>
    /// <param name="traceHeader">The header value from <c>ILambdaContext.TraceId</c>; may be null.</param>
    /// <returns>The root ID, or <c>null</c> when there is none or it is not a well-formed X-Ray ID.</returns>
    public static string? RootId(string? traceHeader)
    {
        if (string.IsNullOrWhiteSpace(traceHeader))
            return null;

        foreach (var part in traceHeader.Split(';'))
        {
            var field = part.Trim(' ');
            if (!field.StartsWith(RootPrefix, StringComparison.Ordinal))
                continue;

            // Only a value that is exactly an X-Ray ID goes into the log scope.
            var root = field[RootPrefix.Length..];
            return WellFormedId().IsMatch(root) ? root : null;
        }

        return null;
    }

    // Version 1, 8 hex digits of epoch seconds, 24 hex digits of randomness. \z, not $: $ would allow a trailing newline.
    [GeneratedRegex(@"\A1-[0-9a-f]{8}-[0-9a-f]{24}\z")]
    private static partial Regex WellFormedId();
}
