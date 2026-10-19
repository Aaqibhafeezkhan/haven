using Haven.Domain;
using Haven.Domain.Enums;

namespace Haven.Application.Common.Interfaces.Hubs;

public interface IServiceStatusNotifier
{
    Task NotifyStatusChangedAsync(Guid serviceId, string serviceName, ServiceDisplayStatus newStatus, CancellationToken cancellationToken = default);
}