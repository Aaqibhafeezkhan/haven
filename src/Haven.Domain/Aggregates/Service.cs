using System.Text.Json;

using Haven.Domain.Entities;
using Haven.Domain.Enums;
using Haven.Domain.Events;
using Haven.Domain.Exceptions;
using Haven.Domain.ValueObjects;

namespace Haven.Domain.Aggregates;

public sealed class Service : AggregateRoot, IDeployableContainer
{
    public Guid EnvironmentId { get; set; }
    public Environment? Environment { get; set; }
    public string Name { get; set; } = default!;
    public string? Alias { get; set; }
    public ServiceType Type { get; set; }
    public ExposureMode ExposureMode { get; set; }
    public DesiredState DesiredState { get; set; } = DesiredState.Stopped;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastDeployedAt { get; set; }
    public string Token { get; set; } = default!;
    public string? SourceConfigJson { get; set; }
    public Guid? GitCredentialId { get; set; } = null;
    public RepositoryBinding? RepositoryBinding { get; set; } = null;

    /// <summary>Whether the repository manifest, not the user, owns this service's declarative definition.</summary>
    public bool IsRepositoryManaged => RepositoryBinding is not null;

    public ServiceSourceConfig? SourceConfig
    {
        get => SourceConfigJson is null ? null : JsonSerializer.Deserialize<ServiceSourceConfig>(SourceConfigJson);
        set => SourceConfigJson = value is null ? null : JsonSerializer.Serialize(value);
    }

    public IReadOnlyList<ServiceNetwork> ServiceNetworks => _serviceNetworks.AsReadOnly();
    private List<ServiceNetwork> _serviceNetworks = [];

    public ICollection<ServiceVolume> Volumes { get; set; } = [];
    public ICollection<ServiceComputedProperty> ComputedProperties { get; set; } = [];

    public ICollection<Deployment> Deployments { get; set; } = [];
    public ICollection<FeatureFlag> FeatureFlags { get; set; } = [];
    public ICollection<HealthCheck> HealthChecks { get; set; } = [];
    public ICollection<CustomAction> CustomActions { get; set; } = [];
    public GitCredentials? GitCredentials { get; set; } = null;

    /// <summary>\
    /// Gets the most recent deployment that is not in a terminal state.
    /// </summary>
    public Deployment? ActiveDeployment => Deployments.FirstOrDefault(d => !d.Status.IsTerminal);

    public static Service Create(Guid environmentId, string name, ServiceType type, ExposureMode exposureMode, string? alias = null, ServiceSourceConfig? sourceConfig = null)
    {
        HavenServiceName.EnsureValidAndNotReserved(name);

        var now = DateTime.UtcNow;
        var service = new Service
        {
            Id = Guid.NewGuid(),
            EnvironmentId = environmentId,
            Name = name,
            Alias = alias,
            Type = type,
            ExposureMode = exposureMode,
            Token = GenerateToken(),
            SourceConfigJson = Serialize(sourceConfig),
            GitCredentialId = ExtractGitCredentialId(sourceConfig),
            DesiredState = DesiredState.Stopped,
            CreatedAt = now,
            UpdatedAt = now
        };

        service.Raise(new ServiceCreatedEvent(service.Id, service.Name));
        return service;
    }

    /// <summary>
    /// Rebuilds a detached, non-persisted snapshot of a service that no longer exists in the
    /// database (e.g. one removed by a restore/sync), carrying only the fields deployment cleanup
    /// needs. This must never be added to the DbContext.
    /// </summary>
    public static Service CreateDetachedSnapshotForCleanup(Guid id, string name, string? alias, ServiceType type, string? sourceConfigJson) =>
        new()
        {
            Id = id,
            Name = name,
            Alias = alias,
            Type = type,
            SourceConfigJson = sourceConfigJson,
            Token = string.Empty,
            DesiredState = DesiredState.Stopped,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    public bool Update(Optional<string> name, Optional<ServiceType> type, Optional<ExposureMode> exposureMode, Optional<string> alias = default, Optional<ServiceSourceConfig?> sourceConfig = default)
    {
        var oldName = Name;
        bool hasChanges = false;

        if (name.HasValue && name.Value != Name)
        {
            HavenServiceName.EnsureValidAndNotReserved(name.Value);
            Name = name.Value;
            hasChanges = true;
        }

        if (alias.HasValue && alias.Value != Alias)
        {
            Alias = alias.Value;
            hasChanges = true;
        }

        if (type.HasValue && type.Value != Type)
        {
            Type = type.Value;
            hasChanges = true;
        }

        if (exposureMode.HasValue && exposureMode.Value != ExposureMode)
        {
            ExposureMode = exposureMode.Value;
            hasChanges = true;
        }

        if (sourceConfig.HasValue)
        {
            SourceConfigJson = Serialize(sourceConfig.Value);
            GitCredentialId = ExtractGitCredentialId(sourceConfig.Value);
            hasChanges = true;
        }

        if (hasChanges)
        {
            UpdatedAt = DateTime.UtcNow;
            Raise(new ServiceUpdatedEvent(Id, oldName, Name));
        }
        return hasChanges;
    }

    /// <summary>
    /// Removes the source config. <see cref="Update"/> can't do this: <see cref="Optional{T}"/> treats null as
    /// "not provided". Returns whether there was anything to remove.
    /// </summary>
    public bool ClearSourceConfig()
    {
        if (SourceConfigJson is null)
            return false;

        SourceConfigJson = null;
        GitCredentialId = null;
        UpdatedAt = DateTime.UtcNow;
        Raise(new ServiceUpdatedEvent(Id, Name, Name));
        return true;
    }

    /// <summary>
    /// Queues a new deployment. Throws if one is already in flight.
    /// </summary>
    public Deployment BeginDeployment(string logFile, string? triggeredBy = null)
    {
        if (ActiveDeployment is not null)
            throw new ValidationException($"Service '{Name}' already has a deployment in progress.");

        var deployment = Deployment.Create(Id, logFile, triggeredBy);
        Deployments.Add(deployment);
        UpdatedAt = DateTime.UtcNow;
        Raise(new ServiceDeployingEvent(Id, Name));
        return deployment;
    }

    /// <summary>Records that a deployment finished successfully and the operator wants the service up.</summary>
    public void CompleteDeployment(Deployment deployment)
    {
        EnsureOwnsDeployment(deployment);
        deployment.Complete();
        OnDeploymentSucceeded();
    }

    /// <summary>
    /// Signals that a deployment started when the <see cref="Deployment"/> row itself is persisted
    /// elsewhere (the deployment log service owns its lifecycle and log file).
    /// </summary>
    public void AnnounceDeploymentStarted()
    {
        UpdatedAt = DateTime.UtcNow;
        Raise(new ServiceDeployingEvent(Id, Name));
    }

    /// <summary>
    /// Applies the service-level effects of a successful deployment: the operator now wants it running.
    /// </summary>
    public void OnDeploymentSucceeded()
    {
        DesiredState = DesiredState.Running;
        var now = DateTime.UtcNow;
        UpdatedAt = now;
        LastDeployedAt = now;
        Raise(new ServiceDeployedEvent(Id, Name));
    }

    public void FailDeployment(Deployment deployment)
    {
        EnsureOwnsDeployment(deployment);
        deployment.Fail();
        UpdatedAt = DateTime.UtcNow;
    }

    public void CancelDeployment(Deployment deployment)
    {
        EnsureOwnsDeployment(deployment);
        deployment.Cancel();
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureOwnsDeployment(Deployment deployment)
    {
        if (!Deployments.Contains(deployment))
            throw new ValidationException("The deployment does not belong to this service.");
    }

    public void RecordDeployment()
    {
        var now = DateTime.UtcNow;
        UpdatedAt = now;
        LastDeployedAt = now;
    }

    /// <summary>Sets operator intent. Observed state is reconciled elsewhere.</summary>
    public void SetDesiredState(DesiredState desiredState)
    {
        if (DesiredState == desiredState) return;
        DesiredState = desiredState;
        UpdatedAt = DateTime.UtcNow;
        if (desiredState == DesiredState.Stopped)
            Raise(new ServiceStoppedEvent(Id, Name));
    }

    public void RegenerateToken()
    {
        Token = GenerateToken();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restart()
    {
        DesiredState = DesiredState.Running;
        UpdatedAt = DateTime.UtcNow;
        Raise(new ServiceRestartedEvent(Id, Name));
    }

    public void ConnectToNetwork(Guid networkId)
    {
        if (!_serviceNetworks.Any(sn => sn.NetworkId == networkId))
            _serviceNetworks.Add(ServiceNetwork.Create(Id, networkId));
    }

    public void DisconnectFromNetwork(Guid networkId)
    {
        var connection = _serviceNetworks.FirstOrDefault(sn => sn.NetworkId == networkId);
        if (connection is not null)
            _serviceNetworks.Remove(connection);
    }


    public void UpdateEnvironmentVariables()
    {
        Raise(new EnvironmentVariablesUpdatedEvent(Id, EnvironmentVariableParentType.Service));
    }

    public static Service Reconstitute(
        Guid id,
        Guid environmentId,
        string name,
        string? alias,
        ServiceType type,
        ExposureMode exposureMode,
        DesiredState desiredState,
        DateTime createdAt,
        DateTime updatedAt,
        ServiceSourceConfig? sourceConfig = null,
        Environment? environment = null,
        IEnumerable<ServiceNetwork>? serviceNetworks = null,
        IEnumerable<ServiceVolume>? volumes = null,
        IEnumerable<ServiceComputedProperty>? computedProperties = null)
    {
        var service = new Service
        {
            Id = id,
            EnvironmentId = environmentId,
            Environment = environment,
            Name = name,
            Alias = alias,
            Type = type,
            ExposureMode = exposureMode,
            DesiredState = desiredState,
            SourceConfigJson = Serialize(sourceConfig),
            GitCredentialId = ExtractGitCredentialId(sourceConfig),
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            _serviceNetworks = serviceNetworks?.ToList() ?? [],
            Volumes = volumes?.ToList() ?? [],
            ComputedProperties = computedProperties?.ToList() ?? []
        };

        if (string.IsNullOrEmpty(service.Token))
            service.Token = GenerateNewToken();

        return service;
    }

    private static string? Serialize(ServiceSourceConfig? config) =>
        config is null ? null : JsonSerializer.Serialize(config);

    /// <summary>
    /// The GitCredentialId FK is kept in sync with the source config's credential selection so that
    /// <c>IGitCredentialsRepository.GetByServiceIdAsync</c> (used by git clone/pull operations) can
    /// resolve the credential without needing to deserialize and inspect the polymorphic SourceConfig.
    /// </summary>
    private static Guid? ExtractGitCredentialId(ServiceSourceConfig? config) =>
        config is DockerfileConfig dockerfileConfig ? dockerfileConfig.GitCredentialId : null;

    public static string GenerateNewToken() =>
        Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string GenerateToken() =>
        GenerateNewToken();

    public void Delete()
    {
        Raise(new ServiceDeletedEvent(Id, Name));
    }

    public FeatureFlag AddFeatureFlag(string name, FeatureFlagType type, string? key, string? description, string value, FeatureFlagValueType valueType)
    {
        var flag = FeatureFlag.Create(Id, name, type, key, description, value, valueType);
        FeatureFlags.Add(flag);
        return flag;
    }

    public void UpdateFeatureFlag(FeatureFlag flag, Optional<string> name, Optional<FeatureFlagType> type, Optional<string?> key, Optional<string?> description, Optional<string> value, Optional<FeatureFlagValueType> valueType)
    {
        if (name.HasValue && name.Value != flag.Name)
            flag.Name = name.Value;

        if (type.HasValue && type.Value != flag.Type)
            flag.Type = type.Value;

        if (key.HasValue && key.Value != flag.Key)
            flag.Key = key.Value;

        if (description.HasValue && description.Value != flag.Description)
            flag.Description = description.Value;

        if (value.HasValue && value.Value != flag.Value)
            flag.Value = value.Value;

        if (valueType.HasValue && valueType.Value != flag.ValueType)
            flag.ValueType = valueType.Value;
    }

    public void RemoveFeatureFlag(FeatureFlag flag)
    {
        FeatureFlags.Remove(flag);
    }

    public ServiceVolume AddVolume(VolumeType type, string name, string target, string? source = null, bool readOnly = false, bool backupEnabled = false)
    {
        EnsureTargetNotTaken(target, excludingVolumeId: null);

        var volume = ServiceVolume.Create(Id, type, name, target, source, readOnly, backupEnabled);
        Volumes.Add(volume);
        UpdatedAt = DateTime.UtcNow;
        Raise(new ServiceUpdatedEvent(Id, Name, Name));
        return volume;
    }

    public void UpdateVolume(ServiceVolume volume, Optional<string> name, Optional<string> source, Optional<string> target, Optional<bool> readOnly, Optional<bool> backupEnabled)
    {
        if (!Volumes.Contains(volume))
            throw new ValidationException("The volume does not belong to this service.");

        if (target.HasValue)
            EnsureTargetNotTaken(target.Value, excludingVolumeId: volume.Id);

        volume.Apply(name, source, target, readOnly, backupEnabled);
        UpdatedAt = DateTime.UtcNow;
        Raise(new ServiceUpdatedEvent(Id, Name, Name));
    }

    /// <summary>
    /// Ensures no other volume on this service already mounts to <paramref name="target"/>,
    /// since Docker rejects two mounts at the same container path.
    /// </summary>
    private void EnsureTargetNotTaken(string target, Guid? excludingVolumeId)
    {
        var trimmedTarget = target?.Trim() ?? string.Empty;
        if (Volumes.Any(v => v.Id != excludingVolumeId && v.Target == trimmedTarget))
            throw new ValidationException($"A volume with target '{trimmedTarget}' already exists on this service.");
    }

    public void RemoveVolume(ServiceVolume volume)
    {
        if (Volumes.Remove(volume))
        {
            UpdatedAt = DateTime.UtcNow;
            Raise(new ServiceUpdatedEvent(Id, Name, Name));
        }
    }

    public ServiceComputedProperty AddComputedProperty(string key, string label, string template, bool isSecret)
    {
        if (ComputedProperties.Any(p => p.Key == key.Trim()))
            throw new ValidationException($"A computed property with key '{key}' already exists on this service.");

        var property = ServiceComputedProperty.Create(Id, key, label, template, isSecret);
        ComputedProperties.Add(property);
        return property;
    }

    public HealthCheck AddHealthCheck(string name, HealthCheckKind kind, bool enabled, string? cronExpression, string config)
    {
        var healthCheck = HealthCheck.Create(Id, name, kind, enabled, cronExpression, config);
        HealthChecks.Add(healthCheck);
        UpdatedAt = DateTime.UtcNow;
        Raise(new ServiceUpdatedEvent(Id, Name, Name));
        return healthCheck;
    }

    public void UpdateHealthCheck(HealthCheck healthCheck, Optional<string> name, Optional<bool> enabled, Optional<string> cronExpression, bool clearCronExpression, Optional<string> config)
    {
        if (!HealthChecks.Contains(healthCheck))
            throw new ValidationException("The health check does not belong to this service.");

        if (name.HasValue)
            healthCheck.Name = name.Value;

        if (enabled.HasValue)
            healthCheck.Enabled = enabled.Value;

        if (clearCronExpression)
            healthCheck.CronExpression = null;
        else if (cronExpression.HasValue)
            healthCheck.CronExpression = cronExpression.Value;

        if (config.HasValue)
            healthCheck.Config = config.Value;

        UpdatedAt = DateTime.UtcNow;
        Raise(new ServiceUpdatedEvent(Id, Name, Name));
    }

    public void RemoveHealthCheck(HealthCheck healthCheck)
    {
        if (HealthChecks.Remove(healthCheck))
        {
            UpdatedAt = DateTime.UtcNow;
            Raise(new ServiceUpdatedEvent(Id, Name, Name));
        }
    }

    /// <summary>
    /// Aggregate of the enabled checks' last results. No enabled checks counts as healthy.
    /// </summary>
    public ServiceHealth ComputeHealthFromChecks()
    {
        var enabledChecks = HealthChecks.Where(hc => hc.Enabled).ToList();
        if (enabledChecks.Count == 0) return ServiceHealth.Healthy;
        if (enabledChecks.Any(hc => hc.LastRunStatus == ServiceHealth.Unhealthy)) return ServiceHealth.Unhealthy;
        if (enabledChecks.Any(hc => hc.LastRunStatus == ServiceHealth.Unknown)) return ServiceHealth.Unknown;
        return ServiceHealth.Healthy;
    }

    /// <summary>
    /// Stores the check result and returns the new aggregate health. The aggregate itself is
    /// observed state and is persisted by the caller on the registry entry.
    /// </summary>
    public ServiceHealth RecordHealthCheckResult(HealthCheck healthCheck, ServiceHealth result)
    {
        if (!HealthChecks.Contains(healthCheck))
            throw new ValidationException("The health check does not belong to this service.");

        var previousHealth = ComputeHealthFromChecks();

        healthCheck.LastRunStatus = result;
        healthCheck.LastRunAt = DateTime.UtcNow;

        var health = ComputeHealthFromChecks();

        if (health == ServiceHealth.Unhealthy && previousHealth != ServiceHealth.Unhealthy)
            Raise(new ServiceDegradedEvent(Id, Name));
        else if (health == ServiceHealth.Healthy && previousHealth == ServiceHealth.Unhealthy)
            Raise(new ServiceRecoveredEvent(Id, Name));

        return health;
    }
}