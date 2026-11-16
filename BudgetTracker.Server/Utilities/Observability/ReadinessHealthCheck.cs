using BudgetTracker.Domain.Interfaces.Managers;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>
/// Adapts <see cref="IReadinessManager"/> to the health-check pipeline behind <c>/health/ready</c>. It reports only
/// healthy or unhealthy: no description and no exception, so neither the response nor the health-check log line can
/// carry a connection string or a driver message.
/// </summary>
public sealed class ReadinessHealthCheck(IReadinessManager readiness, ILogger<ReadinessHealthCheck> logger) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await readiness.IsReadyAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The type only: driver messages can quote the host, database name or user.
            logger.ReadinessCheckFailed(ex.GetType().Name);
            return HealthCheckResult.Unhealthy();
        }
    }
}
