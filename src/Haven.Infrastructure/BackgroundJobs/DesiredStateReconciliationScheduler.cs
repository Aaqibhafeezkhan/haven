using Hangfire;

using Microsoft.Extensions.Hosting;

namespace Haven.Infrastructure.BackgroundJobs;

public sealed class DesiredStateReconciliationScheduler(IRecurringJobManager recurringJobManager) : IHostedService
{
    private const string JobId = "desired-state-reconciliation";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        recurringJobManager.AddOrUpdate<DesiredStateReconciliationJob>(
            JobId,
            job => job.ExecuteAsync(),
            Cron.Minutely());

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}