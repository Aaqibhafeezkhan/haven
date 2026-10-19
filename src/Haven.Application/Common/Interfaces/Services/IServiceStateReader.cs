namespace Haven.Application.Common.Interfaces.Services;

public interface IServiceStateReader
{
    /// <summary>Loads observed state (registry entries) and in-flight deployments for the given services or sidecars in two queries.</summary>
    Task<ServiceStateSnapshot> LoadAsync(IEnumerable<Guid> ownerIds, CancellationToken ct = default);
}
