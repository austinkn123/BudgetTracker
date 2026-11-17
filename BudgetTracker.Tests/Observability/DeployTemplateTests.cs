using System.Text.RegularExpressions;

namespace BudgetTracker.Tests.Observability;

/// <summary>
/// Settings in <c>deploy/template.yaml</c> that the observability plan depends on. The template is only exercised by a
/// real deploy, so these catch an accidental removal before one.
/// </summary>
public class DeployTemplateTests
{
    private static readonly string Template = File.ReadAllText(FindTemplate());

    [Fact]
    public void Should_TurnOnActiveXRayTracing_ForTheFunction() =>
        Assert.Matches(new Regex(@"^      Tracing: Active\s*$", RegexOptions.Multiline), Template);

    [Fact]
    public void Should_KeepLogRetentionSet_SoLogsAreNotKeptForever() =>
        Assert.Matches(new Regex(@"^      RetentionInDays: \d+\s*$", RegexOptions.Multiline), Template);

    // ── Saved Logs Insights queries ─────────────────────────────────────────

    /// <summary>The QueryString of every AWS::Logs::QueryDefinition, keyed by its Name.</summary>
    private static IReadOnlyDictionary<string, string> SavedQueries()
    {
        var queries = new Dictionary<string, string>();
        var definition = new Regex(
            @"Type: AWS::Logs::QueryDefinition\s+Properties:\s+Name: (?<name>[^\n]+)\n(?<body>.*?)QueryString: \|\n(?<query>(?:        [^\n]*\n|\s*\n)+)",
            RegexOptions.Singleline);
        foreach (Match match in definition.Matches(Template))
            queries[match.Groups["name"].Value.Trim()] = match.Groups["query"].Value;
        return queries;
    }

    [Theory]
    [InlineData("BudgetTracker/Find a request by correlation ID")]
    [InlineData("BudgetTracker/Warnings and errors")]
    [InlineData("BudgetTracker/Unhandled exceptions")]
    [InlineData("BudgetTracker/Security events")]
    [InlineData("BudgetTracker/Slow database commands")]
    [InlineData("BudgetTracker/Plaid sync outcomes")]
    [InlineData("BudgetTracker/Webull sync outcomes")]
    public void Should_SaveTheQuery(string name) => Assert.Contains(name, SavedQueries().Keys);

    [Fact]
    public void Should_ScopeEverySavedQueryToTheAppLogGroup()
    {
        var definitions = Regex.Matches(Template, @"Type: AWS::Logs::QueryDefinition").Count;
        var scoped = Regex.Matches(Template, @"LogGroupNames:\s*\n\s+- !Ref BudgetTrackerLogGroup").Count;

        Assert.Equal(SavedQueries().Count, definitions);
        Assert.Equal(definitions, scoped);
    }

    [Fact]
    public void Should_OnlyFilterOnEventIdsThatExist()
    {
        var known = LogCatalogue.All().Select(m => m.Message.EventId).ToHashSet();
        var used = SavedQueries().Values
            .SelectMany(q => Regex.Matches(q, @"EventId (?:=|in) \[?(?<ids>[\d, ]+)\]?").Select(m => m.Groups["ids"].Value))
            .SelectMany(ids => ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(int.Parse)
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, id => Assert.Contains(id, known));
    }

    [Fact]
    public void Should_OnlyReadStateFieldsThatSomeLogMessageWrites()
    {
        var known = LogCatalogue.All().SelectMany(m => m.Fields).ToHashSet();
        var used = SavedQueries().Values
            .SelectMany(q => Regex.Matches(q, @"State\.(\w+)").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, field => Assert.Contains(field, known));
    }

    [Fact]
    public void Should_FilterSecurityEventsOnTheRealCategory() =>
        Assert.Contains($"Category = \"{BudgetTracker.Server.Utilities.Observability.SecurityLog.Category}\"",
            SavedQueries()["BudgetTracker/Security events"]);

    [Fact]
    public void Should_FindARequestByTheScopeKeyTheAppWrites() =>
        Assert.Contains($"\"{BudgetTracker.Server.Utilities.Observability.RequestLogScope.CorrelationIdKey}\":",
            SavedQueries()["BudgetTracker/Find a request by correlation ID"]);

    private static string FindTemplate()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "deploy", "template.yaml");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("deploy/template.yaml was not found above the test output directory.");
    }
}
