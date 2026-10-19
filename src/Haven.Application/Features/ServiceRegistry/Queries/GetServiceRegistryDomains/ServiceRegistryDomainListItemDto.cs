using Haven.Domain.Enums;

namespace Haven.Application.Features.ServiceRegistry.Queries.GetServiceRegistryDomains;

/// <summary>A domain together with the service/sidecar that serves it.</summary>
public sealed class ServiceRegistryDomainListItemDto
{
    public Guid Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public int ContainerPort { get; set; }
    public TlsMode TlsMode { get; set; }
    public bool HasCertificate { get; set; }
    public string? CertificateName { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? ContainerName { get; set; }
    public ServiceRuntimeState RuntimeState { get; set; }
    public ServiceHealth Health { get; set; }
    public Guid? ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public string? SidecarName { get; set; }
    public string? EnvironmentName { get; set; }
    public string? ProjectName { get; set; }
}