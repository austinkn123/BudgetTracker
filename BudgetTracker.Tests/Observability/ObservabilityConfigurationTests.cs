using BudgetTracker.Server.Utilities.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Tests.Observability;

public class ObservabilityConfigurationTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void Should_WriteJsonConsoleLogsWithScopes(string environment)
    {
        using var services = BuildServices(environment);

        Assert.Equal(ConsoleFormatterNames.Json, services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue.FormatterName);
        var json = services.GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>().CurrentValue;
        Assert.True(json.IncludeScopes);
        Assert.True(json.UseUtcTimestamp);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command")]
    [InlineData("System.Net.Http.HttpClient")]
    public void Should_CapSqlAndOutboundHttpLoggingAtWarning_When_NotDevelopment(string category)
    {
        using var services = BuildServices("Production");

        var filters = services.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;

        Assert.Contains(filters.Rules, r => r.CategoryName == category && r.ProviderName is null && r.LogLevel == LogLevel.Warning);
    }

    [Fact]
    public void Should_NotCapSqlLogging_When_Development()
    {
        using var services = BuildServices("Development");

        var filters = services.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;

        Assert.DoesNotContain(filters.Rules, r => r.CategoryName == "Microsoft.EntityFrameworkCore.Database.Command" && r.LogLevel == LogLevel.Warning);
    }

    private static ServiceProvider BuildServices(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.AddBudgetTrackerObservability();
        return builder.Services.BuildServiceProvider();
    }
}
