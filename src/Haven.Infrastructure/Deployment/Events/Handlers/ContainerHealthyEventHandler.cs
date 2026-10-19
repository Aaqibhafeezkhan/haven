using Haven.Application.Common.Interfaces.Repositories;
using Haven.Domain;
using Haven.Domain.Enums;
using Haven.Infrastructure.Persistence;

using Mediator;

using Microsoft.Extensions.Logging;

namespace Haven.Infrastructure.Deployment.Events.Handlers;

public class ContainerHealthyEventHandler : INotificationHandler<ContainerHealthyEvent>
{
    private readonly HavenDbContext _db;
    private readonly IProjectRepository _repository;
    private readonly ISidecarRepository _sidecarRepository;
    private readonly ServiceRuntimeStateRecorder _recorder;
    private readonly ILogger<ContainerHealthyEventHandler> _logger;

    public ContainerHealthyEventHandler(HavenDbContext db, IProjectRepository repository, ISidecarRepository sidecarRepository,
        ServiceRuntimeStateRecorder recorder,
        ILogger<ContainerHealthyEventHandler> logger)
    {
        _db = db;
        _repository = repository;
        _sidecarRepository = sidecarRepository;
        _recorder = recorder;
        _logger = logger;
    }

    public async ValueTask Handle(ContainerHealthyEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Container {ContainerId} is now healthy. Marking service {ServiceId} as running",
            notification.ContainerId, notification.ServiceId);

        var project = await _repository.GetByServiceIdAsync(notification.ServiceId, cancellationToken);
        if (project is not null)
        {
            var service = project.Environments
                .SelectMany(e => e.Services)
                .FirstOrDefault(s => s.Id == notification.ServiceId);

            if (service == null)
            {
                _logger.LogWarning("Service {ServiceId} not found in project {ProjectId}", notification.ServiceId,
                    project.Id);
                return;
            }

            await _recorder.ApplyRunningAsync(service, cancellationToken);
            await _recorder.ApplyHealthAsync(service, ServiceHealth.Healthy, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Service {ServiceId} reported healthy", notification.ServiceId);
            return;
        }

        var sidecar = await _sidecarRepository.GetByIdAsync(notification.ServiceId, cancellationToken);
        if (sidecar is null)
        {
            _logger.LogWarning("No service or sidecar found for container id {Id}", notification.ServiceId);
            return;
        }

        await _recorder.ApplyRunningAsync(sidecar, cancellationToken);
        await _recorder.ApplyHealthAsync(sidecar, ServiceHealth.Healthy, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Sidecar {SidecarId} reported healthy", sidecar.Id);
    }
}