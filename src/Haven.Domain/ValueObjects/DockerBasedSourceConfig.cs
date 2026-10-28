using Haven.Domain.Enums;

namespace Haven.Domain.ValueObjects;

public abstract class DockerBasedSourceConfig : ServiceSourceConfig
{
    public List<string> Ports { get; set; } = [];
    public List<string> CommandArgs { get; set; } = [];
    public RestartPolicy RestartPolicy { get; set; } = RestartPolicy.UnlessStopped;
}