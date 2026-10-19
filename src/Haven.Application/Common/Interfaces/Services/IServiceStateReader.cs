namespace Haven.Application.Common.Interfaces.Services;

public interface IServiceStateReader
{
    /// <summary>Loads observed state (registry entries) and in-flight deployments for the given services in two queries.</summary>
    Task<ServiceStateSnapshot> LoadAsync(IEnumerable<Guid> serviceIds, CancellationToken ct = default);
}
