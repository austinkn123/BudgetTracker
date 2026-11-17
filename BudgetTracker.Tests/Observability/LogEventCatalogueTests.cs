using System.Reflection;
using BudgetTracker.Domain.Common;
using BudgetTracker.Server.Utilities.Observability;
using Microsoft.Extensions.Logging;

namespace BudgetTracker.Tests.Observability;

/// <summary>
/// Every log message is source-generated with a stable event ID inside its area's block, so CloudWatch queries can
/// filter on <c>EventId</c> rather than message text.
/// </summary>
public class LogEventCatalogueTests
{
    private static readonly (Type Catalogue, int Base)[] Catalogues =
    [
        (typeof(ObservabilityLog), LogEvents.ObservabilityBase),
        (typeof(PlaidLog), LogEvents.PlaidBase),
        (typeof(WebullLog), LogEvents.WebullBase),
        (typeof(DataLog), LogEvents.DataBase),
    ];

    private static IEnumerable<(Type Catalogue, int Base, MethodInfo Method, LoggerMessageAttribute Message)> Messages() =>
        from c in Catalogues
        from method in c.Catalogue.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        let message = method.GetCustomAttribute<LoggerMessageAttribute>()
        where message is not null
        select (c.Catalogue, c.Base, method, message);

    [Fact]
    public void Should_FindTheWholeCatalogue() => Assert.Equal(29, Messages().Count());

    [Fact]
    public void Should_GiveEveryMessageAUniqueEventId()
    {
        var duplicates = Messages().GroupBy(m => m.Message.EventId).Where(g => g.Count() > 1).Select(g => g.Key);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Should_KeepEveryEventIdInsideItsAreaBlock()
    {
        Assert.All(Messages(), m =>
            Assert.InRange(m.Message.EventId, m.Base + 1, m.Base + 999));
    }

    [Fact]
    public void Should_GiveEveryMessageATemplateAndAnExplicitLevel()
    {
        Assert.All(Messages(), m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Message.Message), $"{m.Method.Name} has no message template.");
            Assert.NotEqual(LogLevel.None, m.Message.Level);
        });
    }

    [Fact]
    public void Should_NeverPutAnExceptionMessageInATemplate()
    {
        // Exceptions travel as the exception argument, or as a type name; a {Message} field would bypass that.
        Assert.All(Messages(), m => Assert.DoesNotContain("{Message}", m.Message.Message));
    }
}
