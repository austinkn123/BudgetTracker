using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Plaid;
using Microsoft.Extensions.Options;

namespace BudgetTracker.Server.Services;

/// <summary>
/// Backup sweep: on a fixed interval, re-syncs every active Plaid item so stale data never
/// persists even if a webhook is missed. Each tick runs in its own DI scope (the manager and its
/// accessors are scoped) and is wrapped so a failed tick logs and continues rather than killing the loop.
/// </summary>
public class PlaidSyncSweepService(
    IServiceScopeFactory scopeFactory,
    IOptions<PlaidOptions> options,
    ILogger<PlaidSyncSweepService> logger) : BackgroundService
{
    private readonly PlaidOptions _options = options.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Guard against a misconfigured non-positive interval that would throw in PeriodicTimer.
        var intervalHours = _options.SweepIntervalHours > 0 ? _options.SweepIntervalHours : 6;
        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var manager = scope.ServiceProvider.GetRequiredService<IPlaidManager>();
                await manager.SweepAllAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown — swallow and exit the loop.
                break;
            }
            catch (Exception ex)
            {
                // A failed tick must never terminate the loop.
                logger.LogError(ex, "Plaid sweep tick failed; will retry on the next interval.");
            }
        }
    }
}
