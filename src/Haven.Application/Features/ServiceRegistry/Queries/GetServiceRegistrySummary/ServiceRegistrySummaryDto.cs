namespace Haven.Application.Features.ServiceRegistry.Queries.GetServiceRegistrySummary;

public sealed class ServiceRegistrySummaryDto
{
    public int Total { get; set; }
    public int Running { get; set; }
    public int Stopped { get; set; }
    public int Starting { get; set; }
    public int Unknown { get; set; }
    public int DomainCount { get; set; }
    public int ExposedExternally { get; set; }
}