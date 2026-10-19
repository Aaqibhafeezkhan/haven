using Haven.Application.Common.Interfaces.Repositories;
using Haven.Domain;
using Haven.Domain.Enums;
using Haven.Infrastructure.Persistence;

using Mediator;

using Microsoft.Extensions.Logging;

namespace Haven.Infrastructure.Deployment.Events.Handlers;

public class ContainerOutOfMemoryEventHandler : INotificationHandler<ContainerOutOfMemoryEvent>
{
    private readonly HavenDbContext _db;
    private readonly IProjectRepository _repository;
    private readonly ISidecarRepository _sidecarRepository;
    private readonly ServiceRuntimeStateRecorder _recorder;
    private readonly ILogger<ContainerOutOfMemoryEventHandler> _logger;

    public ContainerOutOfMemoryEventHandler(HavenDbContext db, IProjectRepository repository, ISidecarRepository sidecarRepository, ServiceRuntimeStateRecorder recorder, ILogger<ContainerOutOfMemoryEventHandler> logger)
    {
        _db = db;
        _repository = repository;
        _sidecarRepository = sidecarRepository;
        _recorder = recorder;
        _logger = logger;
    }

    public async ValueTask Handle(ContainerOutOfMemoryEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogError("Container {ContainerId} ran out of memory. Marking service {ServiceId} as stopped",
            notification.ContainerId, notification.ServiceId);

        var project = await _repository.GetByServiceIdAsync(notification.ServiceId, cancellationToken);
        if (project is not null)
        {
            var service = project.Environments
                .SelectMany(e => e.Services)
                .FirstOrDefault(s => s.Id == notification.ServiceId);

            if (service == null)
            {
                _logger.LogWarning("Service {ServiceId} not found in project {ProjectId}", notification.ServiceId, project.Id);
                return;
            }

            if (!await _recorder.ApplyStopAsync(service, ServiceRuntimeState.Crashed, null, cancellationToken))
            {
                _logger.LogInformation("Service {ServiceId} is already stopped or being deployed, skipping stop handler", notification.ServiceId);
                return;
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Service {ServiceId} marked as stopped due to out-of-memory error", notification.ServiceId);
            return;
        }

        var sidecar = await _sidecarRepository.GetByIdAsync(notification.ServiceId, cancellationToken);
        if (sidecar is null)
        {
            _logger.LogWarning("No service or sidecar found for container id {Id}", notification.ServiceId);
            return;
        }

        if (!await _recorder.ApplyStopAsync(sidecar, ServiceRuntimeState.Crashed, null, cancellationToken))
        {
            _logger.LogInformation("Sidecar {SidecarId} is already stopped or restarting, skipping stop handler", sidecar.Id);
            return;
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sidecar {SidecarId} marked as stopped due to out-of-memory error", sidecar.Id);
    }
}