using Haven.Infrastructure.Deployment;

using Microsoft.Extensions.Logging;

namespace Haven.Infrastructure.BackgroundJobs;

public sealed class DesiredStateReconciliationJob(
    DesiredStateReconciler reconciler,
    ILogger<DesiredStateReconciliationJob> logger)
{
    public async Task ExecuteAsync()
    {
        try
        {
            await reconciler.ReconcileAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Desired state reconciliation run failed; will retry on the next schedule");
        }
    }
}
