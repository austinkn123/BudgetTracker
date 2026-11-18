using System.Text.RegularExpressions;

namespace BudgetTracker.Tests.Observability;

/// <summary>
/// Settings in <c>deploy/template.yaml</c> that the observability plan depends on. The template is only exercised by a
/// real deploy, so these catch an accidental removal before one.
/// </summary>
public class DeployTemplateTests
{
    // A Windows checkout has CRLF endings; the patterns below match on \n.
    private static readonly string Template = File.ReadAllText(FindTemplate()).ReplaceLineEndings("\n");

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

    // ── Email alerts ────────────────────────────────────────────────────────

    /// <summary>The indented block of one top-level resource, from its logical ID to the next resource.</summary>
    private static string Resource(string logicalId)
    {
        var match = Regex.Match(Template, $@"^  {logicalId}:\n(?<body>(?:    .*\n|\s*\n)+)", RegexOptions.Multiline);
        Assert.True(match.Success, $"Resource {logicalId} is missing from deploy/template.yaml.");
        return match.Groups["body"].Value;
    }

    [Fact]
    public void Should_TakeTheAlertEmailAsAParameter_NotFromTheRepo()
    {
        Assert.Matches(new Regex(@"^  AlertEmail:\n    Type: String\n    Default: ''", RegexOptions.Multiline), Template);
        // No address is committed: it is supplied at deploy time.
        Assert.DoesNotMatch(new Regex(@"[\w.+-]+@[\w-]+\.[\w.]+"), Template);
    }

    [Fact]
    public void Should_EmailTheTopicOnlyWhenAnAddressIsGiven()
    {
        Assert.Contains("Type: AWS::SNS::Topic", Resource("AlertTopic"));
        var subscription = Resource("AlertEmailSubscription");
        Assert.Contains("Type: AWS::SNS::Subscription", subscription);
        Assert.Contains("Condition: HasAlertEmail", subscription);
        Assert.Contains("Protocol: email", subscription);
        Assert.Contains("Endpoint: !Ref AlertEmail", subscription);
        Assert.Matches(new Regex(@"HasAlertEmail: !Not \[!Equals \[!Ref AlertEmail, ''\]\]"), Template);
    }

    [Fact]
    public void Should_AlarmOnLambdaErrors_ToTheAlertTopic()
    {
        var alarm = Resource("FunctionErrorsAlarm");
        Assert.Contains("Type: AWS::CloudWatch::Alarm", alarm);
        Assert.Contains("Namespace: AWS/Lambda", alarm);
        Assert.Contains("MetricName: Errors", alarm);
        Assert.Matches(new Regex(@"Name: FunctionName\n\s+Value: !Ref BudgetTrackerFunction"), alarm);
        Assert.Contains("- !Ref AlertTopic", alarm);
        Assert.Contains("TreatMissingData: notBreaching", alarm);
    }

    [Fact]
    public void Should_CountServerErrors_FromTheUnhandledExceptionEvent()
    {
        var filter = Resource("ServerErrorsMetricFilter");
        Assert.Contains("Type: AWS::Logs::MetricFilter", filter);
        Assert.Contains("LogGroupName: !Ref BudgetTrackerLogGroup", filter);
        var eventId = int.Parse(Regex.Match(filter, @"\$\.EventId = (\d+)").Groups[1].Value);
        // The event the exception handler writes for every 500 response.
        var unhandled = Assert.Single(LogCatalogue.All(), m => m.Method.Name == "UnhandledException");
        Assert.Equal(unhandled.Message.EventId, eventId);

        var metric = Regex.Match(filter, @"MetricName: (\S+)").Groups[1].Value;
        var alarm = Resource("ServerErrorsAlarm");
        Assert.Contains($"MetricName: {metric}", alarm);
        Assert.Contains("- !Ref AlertTopic", alarm);
        Assert.Contains("TreatMissingData: notBreaching", alarm);
    }

    [Fact]
    public void Should_MakeTheCostBudgetOptIn()
    {
        Assert.Matches(new Regex(@"^  MonthlyBudgetUsd:\n    Type: Number\n    Default: 0", RegexOptions.Multiline), Template);
        var budget = Resource("MonthlyCostBudget");
        Assert.Contains("Type: AWS::Budgets::Budget", budget);
        Assert.Contains("Condition: HasBudgetAndEmail", budget);
        Assert.Contains("Amount: !Ref MonthlyBudgetUsd", budget);
        Assert.Contains("Address: !Ref AlertEmail", budget);
    }

    // ── Scheduled Webull close snapshot ─────────────────────────────────────

    [Fact]
    public void Should_DefineTheScheduledSnapshotFunction_WithTheEntryProgramChecks()
    {
        var function = Resource("WebullCloseSnapshotFunction");
        Assert.Contains("Type: AWS::Serverless::Function", function);
        Assert.Contains($"BUDGETTRACKER_ENTRY: {BudgetTracker.Server.Services.WebullCloseSnapshotService.LambdaEntry}", function);
    }

    [Fact]
    public void Should_RunTheSnapshotFromTheSamePackageAndRuntimeAsTheApp()
    {
        string Value(string body, string key) => Regex.Match(body, $@"^      {key}: (.+)$", RegexOptions.Multiline).Groups[1].Value.Trim();
        var app = Resource("BudgetTrackerFunction");
        var snapshot = Resource("WebullCloseSnapshotFunction");

        foreach (var key in new[] { "CodeUri", "Handler", "Runtime", "Architectures" })
            Assert.Equal(Value(app, key), Value(snapshot, key));
        Assert.Contains("ASPNETCORE_ENVIRONMENT: !Ref AspNetCoreEnvironment", snapshot);
        Assert.Contains("SsmParameterPath: !Ref SsmParameterPath", snapshot);
        Assert.Contains("ssm:GetParametersByPath", snapshot);
    }

    [Fact]
    public void Should_ScheduleTheSnapshotOnWeekdaysAfterTheNewYorkClose()
    {
        var function = Resource("WebullCloseSnapshotFunction");
        Assert.Contains("Type: ScheduleV2", function);
        Assert.Contains("ScheduleExpressionTimezone: America/New_York", function);
        var cron = Regex.Match(function, @"ScheduleExpression: cron\((?<min>\d+) (?<hours>[\d,-]+) \? \* (?<days>\S+) \*\)");
        Assert.True(cron.Success, "Expected a cron(...) ScheduleExpression.");
        Assert.Equal("MON-FRI", cron.Groups["days"].Value);
        // The engine treats 4:30 pm Eastern as the close; the first run must come after it.
        var firstHour = int.Parse(cron.Groups["hours"].Value.Split('-', ',')[0]);
        var firstMinute = int.Parse(cron.Groups["min"].Value);
        Assert.True(firstHour * 60 + firstMinute > 16 * 60 + 30, "The first run is at or before the 4:30 pm close.");
    }

    [Fact]
    public void Should_GiveTheSnapshotNoPublicUrl() =>
        Assert.DoesNotContain("FunctionUrlConfig", Resource("WebullCloseSnapshotFunction"));

    [Fact]
    public void Should_KeepTheSnapshotsLogsFor14Days_AndAlarmOnItsErrors()
    {
        var logGroup = Resource("WebullCloseSnapshotLogGroup");
        Assert.Contains("LogGroupName: !Sub /aws/lambda/${WebullCloseSnapshotFunction}", logGroup);
        Assert.Contains("RetentionInDays: 14", logGroup);

        var alarm = Resource("WebullCloseSnapshotErrorsAlarm");
        Assert.Contains("MetricName: Errors", alarm);
        Assert.Matches(new Regex(@"Name: FunctionName\n\s+Value: !Ref WebullCloseSnapshotFunction"), alarm);
        Assert.Contains("- !Ref AlertTopic", alarm);
    }

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
