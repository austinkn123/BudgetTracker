using BudgetTracker.Domain.Interfaces.Managers;

namespace BudgetTracker.Server.Services;

/// <summary>
/// Local stand-in for the Lambda's EventBridge schedule: runs the Webull market-close snapshot once at startup (catching
/// up after the machine was off), then every 10 minutes. <see cref="IWebullManager.RunCloseSnapshotsAsync"/> decides
/// whether a fetch is due, so frequent ticks only cost a database read. Each tick gets its own DI scope; a failed tick
/// logs and the loop continues.
/// </summary>
public class WebullCloseSnapshotService(
    IServiceScopeFactory scopeFactory,
    ILogger<WebullCloseSnapshotService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // don't hold up host startup with the first tick's I/O
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IWebullManager>().RunCloseSnapshotsAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Webull close snapshot tick failed; will retry on the next interval.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
