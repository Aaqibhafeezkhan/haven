using Haven.Domain.Enums;

namespace Haven.Domain.ValueObjects;

public abstract class DockerBasedSourceConfig : ServiceSourceConfig
{
    public List<string> Ports { get; set; } = [];
    public List<string> CommandArgs { get; set; } = [];
    public RestartPolicy RestartPolicy { get; set; } = RestartPolicy.UnlessStopped;
    
    /// <summary>
    /// Gets or sets the memory limit for the Docker container in bytes. A value of 0 indicates no limit.
    /// </summary>
    public long MemoryLimit { get; set; } = 0;

    /// <summary>
    /// Gets or sets the memory swap limit for the Docker container in bytes. A value of -1 indicates that the swap limit is disabled.
    /// </summary>
    public long MemorySwap { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the memory reservation for the Docker container in bytes. A value of 0 indicates no reservation.
    /// </summary>
    public long MemoryReservation { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the CPU limit for the Docker container in nano cpus. A value of 0 indicates no limit.
    /// </summary>
    public long NanoCpus { get; set; } = 0;
}