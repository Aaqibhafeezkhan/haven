using Haven.Application.Features.Services;

namespace Haven.Application.Features.ServiceTemplates.Queries.GetServiceTemplateById;

public sealed class ServiceTemplateDto
{
    public string Id { get; set; } = null!;
    public string Version { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Icon { get; set; } = null!;
    public string Category { get; set; } = null!;
    public List<TemplateInputFieldDto> Inputs { get; set; } = new();

    /// <summary>The template's default resource limits, or null when it does not define any.</summary>
    public ServiceResourceLimits? ResourceLimits { get; set; }
}