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
