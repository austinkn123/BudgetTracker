using System.Reflection;
using System.Text.RegularExpressions;
using BudgetTracker.Domain.Common;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.Extensions.Logging;

namespace BudgetTracker.Tests.Observability;

/// <summary>Every source-generated log message in the app, read by reflection from the four catalogue classes.</summary>
internal static partial class LogCatalogue
{
    private static readonly (Type Catalogue, int Base)[] Catalogues =
    [
        (typeof(ObservabilityLog), LogEvents.ObservabilityBase),
        (typeof(PlaidLog), LogEvents.PlaidBase),
        (typeof(WebullLog), LogEvents.WebullBase),
        (typeof(DataLog), LogEvents.DataBase),
    ];

    /// <summary>One log message: where it is declared, its area's base event ID, and its attribute.</summary>
    public sealed record Entry(Type Catalogue, int Base, MethodInfo Method, LoggerMessageAttribute Message)
    {
        /// <summary>The structured field names in the message template, e.g. <c>DurationMs</c>.</summary>
        public IEnumerable<string> Fields => Placeholder().Matches(Message.Message).Select(m => m.Groups[1].Value);
    }

    public static IReadOnlyList<Entry> All() =>
        (from c in Catalogues
         from method in c.Catalogue.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
         let message = method.GetCustomAttribute<LoggerMessageAttribute>()
         where message is not null
         select new Entry(c.Catalogue, c.Base, method, message)).ToList();

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();
}
