using BudgetTracker.Server.Utilities.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
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
    public void Should_CapSqlOutboundHttpAndHostingLoggingAtWarning_When_NotDevelopment(string category)
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

    [Fact]
    public void Should_CapAspNetCoreHostingLoggingAtWarning_When_NotDevelopment_EvenIfConfigurationLowersIt()
    {
        // Hosting logs full URLs, query strings included, at Information.
        using var services = BuildServices("Production", new() { ["Logging:LogLevel:Microsoft.AspNetCore"] = "Information" });

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
    }

    [Fact]
    public void Should_NotCapAspNetCoreHostingLogging_When_Development()
    {
        using var services = BuildServices("Development", new() { ["Logging:LogLevel:Microsoft.AspNetCore"] = "Information" });

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");

        Assert.True(logger.IsEnabled(LogLevel.Information));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void Should_SilenceBuiltInExceptionHandlerLogging_InEveryEnvironment(string environment)
    {
        using var services = BuildServices(environment);

        var filters = services.GetRequiredService<IOptions<LoggerFilterOptions>>().Value;

        Assert.Contains(filters.Rules, r =>
            r.CategoryName == "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware"
            && r.ProviderName is null && r.LogLevel == LogLevel.None);
    }

    private static ServiceProvider BuildServices(string environment, Dictionary<string, string?>? configuration = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        if (configuration is not null)
            builder.Configuration.AddInMemoryCollection(configuration);
        builder.AddBudgetTrackerObservability();
        return builder.Services.BuildServiceProvider();
    }
}
