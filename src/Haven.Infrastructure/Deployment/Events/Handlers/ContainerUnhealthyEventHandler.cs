using Haven.Application.Common.Interfaces.Repositories;
using Haven.Domain;
using Haven.Domain.Enums;
using Haven.Infrastructure.Persistence;

using Mediator;

using Microsoft.Extensions.Logging;

namespace Haven.Infrastructure.Deployment.Events.Handlers;

public class ContainerUnhealthyEventHandler : INotificationHandler<ContainerUnhealthyEvent>
{
    private readonly HavenDbContext _db;
    private readonly IProjectRepository _repository;
    private readonly ISidecarRepository _sidecarRepository;
    private readonly ServiceRuntimeStateRecorder _recorder;
    private readonly ILogger<ContainerUnhealthyEventHandler> _logger;

    public ContainerUnhealthyEventHandler(HavenDbContext db, IProjectRepository repository, ISidecarRepository sidecarRepository,
        ServiceRuntimeStateRecorder recorder,
        ILogger<ContainerUnhealthyEventHandler> logger)
    {
        _db = db;
        _repository = repository;
        _sidecarRepository = sidecarRepository;
        _recorder = recorder;
        _logger = logger;
    }

    public async ValueTask Handle(ContainerUnhealthyEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Container {ContainerId} reported unhealthy. Recording health for service {ServiceId}",
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

            await _recorder.ApplyHealthAsync(service, ServiceHealth.Unhealthy, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Service {ServiceId} reported unhealthy", notification.ServiceId);
            return;
        }

        var sidecar = await _sidecarRepository.GetByIdAsync(notification.ServiceId, cancellationToken);
        if (sidecar is null)
        {
            _logger.LogWarning("No service or sidecar found for container id {Id}", notification.ServiceId);
            return;
        }

        await _recorder.ApplyHealthAsync(sidecar, ServiceHealth.Unhealthy, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Sidecar {SidecarId} reported unhealthy", sidecar.Id);
    }
}