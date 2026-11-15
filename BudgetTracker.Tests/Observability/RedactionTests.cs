using BudgetTracker.Server.Utilities.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BudgetTracker.Tests.Observability;

/// <summary>
/// Classified log fields through the real logging configuration. No production log line carries a classified field
/// today, so the messages under test are samples; they prove the safety net works before one is ever needed.
/// </summary>
public partial class RedactionTests
{
    private const string Payee = "ACME GROCERY 0042";
    private const string Token = "access-sandbox-SECRET";
    private const string Email = "someone@example.com";

    [Fact]
    public async Task Should_EraseClassifiedFields_AndKeepUnclassifiedOnes()
    {
        await using var host = await ObservabilityTestHost.StartAsync(_ => { });
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Sample");

        LogSample(logger, Payee, Token, Email, userId: 42);

        var record = Assert.Single(host.Logs.GetSnapshot(), r => r.Category == "Sample");
        var rendered = ObservabilityTestHost.Render(record);
        Assert.DoesNotContain(Payee, rendered);
        Assert.DoesNotContain(Token, rendered);
        Assert.DoesNotContain(Email, rendered);
        Assert.Contains("UserId=42", rendered);
        // The redaction pipeline is on: each classified field is written, but empty. (With it off the fields are
        // dropped instead, which is also safe; this pins the configured behaviour.)
        var state = record.StructuredState!.ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Equal(string.Empty, state["Payee"]);
        Assert.Equal(string.Empty, state["Token"]);
        Assert.Equal(string.Empty, state["Email"]);
        Assert.Equal("Saw  with  for , user 42", record.Message);
    }

    [Fact]
    public async Task Should_EraseClassifiedFieldsInDevelopmentToo()
    {
        await using var host = await ObservabilityTestHost.StartAsync(_ => { }, environment: "Development");
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Sample");

        LogSample(logger, Payee, Token, Email, userId: 42);

        var record = Assert.Single(host.Logs.GetSnapshot(), r => r.Category == "Sample");
        Assert.DoesNotContain(Payee, ObservabilityTestHost.Render(record));
    }

    [LoggerMessage(EventId = 9001, Level = LogLevel.Information, Message = "Saw {Payee} with {Token} for {Email}, user {UserId}")]
    private static partial void LogSample(
        ILogger logger,
        [FinancialData] string payee,
        [CredentialData] string token,
        [PersonalData] string email,
        int userId);
}
