using System.Collections.Concurrent;

using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Interfaces.Hubs;
using Haven.Application.Common.Interfaces.Repositories;
using Haven.Application.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Haven.Infrastructure.Deployment;

public class DeploymentLogService(
    IServiceScopeFactory scopeFactory,
    IDeploymentLogNotifier notifier,
    IOptionsMonitor<InstanceOptions> instanceOptions) : IDeploymentLogService
{
    private readonly ConcurrentDictionary<Guid, LogWriter> _writers = new();

    public async Task<Domain.Entities.Deployment> CreateDeploymentForServiceAsync(Guid serviceId, CancellationToken ct)
    {
        var baseLogFilePath = instanceOptions.CurrentValue.DeploymentLogBasePath;
        var logFilePath = $"{baseLogFilePath}/{serviceId}_{DateTime.UtcNow:yyyyMMddHHmmss}.log";
        var deployment = Domain.Entities.Deployment.Create(serviceId, logFilePath);

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await repository.AddAsync(deployment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        Directory.CreateDirectory(baseLogFilePath);
        var writer = new LogWriter(new StreamWriter(
            new FileStream(logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        });
        _writers[deployment.Id] = writer;

        return deployment;
    }

    public async Task AppendLogAsync(Guid deploymentId, string logEntry, CancellationToken ct)
    {
        var timestamp = DateTime.UtcNow;

        if (_writers.TryGetValue(deploymentId, out var writer))
            await writer.WriteLineAsync($"[{timestamp:yyyy-MM-dd HH:mm:ss}] {logEntry}");

        await notifier.SendLogEntryAsync(deploymentId, logEntry, ct);
    }

    public async Task MarkDeploymentCompletedAsync(Guid deploymentId, CancellationToken ct)
    {
        await FlushAndClose(deploymentId);

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var deployment = await repository.FindByIdAsync(deploymentId, ct);
        if (deployment is null) return;

        deployment.Complete();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task MarkDeploymentFailedAsync(Guid deploymentId, CancellationToken ct)
    {
        await FlushAndClose(deploymentId);

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var deployment = await repository.FindByIdAsync(deploymentId, ct);
        if (deployment is null) return;

        deployment.Fail();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task MarkDeploymentCancelledAsync(Guid deploymentId, CancellationToken ct)
    {
        await FlushAndClose(deploymentId);

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var deployment = await repository.FindByIdAsync(deploymentId, ct);
        if (deployment is null) return;

        deployment.Cancel();
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task FlushAndClose(Guid deploymentId)
    {
        if (_writers.TryRemove(deploymentId, out var writer))
            await writer.DisposeAsync();
    }

    /// <summary>
    /// Serializes writes to a <see cref="StreamWriter"/>, which does not allow concurrent async operations.
    /// Callers (e.g. Docker build progress callbacks) may append from multiple threads at once.
    /// </summary>
    private sealed class LogWriter(StreamWriter inner)
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private bool _disposed;

        public async Task WriteLineAsync(string line)
        {
            await _gate.WaitAsync();
            try
            {
                if (_disposed) return;
                await inner.WriteLineAsync(line);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task DisposeAsync()
        {
            await _gate.WaitAsync();
            try
            {
                if (_disposed) return;
                _disposed = true;
                await inner.DisposeAsync();
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}