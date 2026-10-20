using Docker.DotNet;
using Docker.DotNet.Models;

using Haven.Application.Common;
using Haven.Application.Common.Contracts;
using Haven.Application.Common.Interfaces;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Templating;
using Haven.Domain.Aggregates;
using Haven.Domain.Enums;
using Haven.Domain.ValueObjects;
using Haven.Infrastructure.Utils;

using Microsoft.Extensions.Logging;

using Environment = Haven.Domain.Aggregates.Environment;

namespace Haven.Infrastructure.Deployment.Docker;

/// <summary>Deploys services built from a <see cref="DockerfileConfig"/> (raw content or a Git-hosted Dockerfile).</summary>
public class DockerfileDeployService : IDeployService
{
    private readonly ILogger<DockerfileDeployService> _logger;
    private readonly IDockerClient _dockerClient;
    private readonly IDockerContainerRuntime _containerRuntime;
    private readonly INetworkingService _networkingService;
    private readonly IGitService _gitService;
    private readonly IDeploymentLogService _logService;
    private readonly IServiceTemplateNamespaceProvider _templateNamespaceProvider;
    private readonly IRepositoryLockProvider _repositoryLocks;

    public DockerfileDeployService(
        ILogger<DockerfileDeployService> logger,
        IDockerClient dockerClient,
        IDockerContainerRuntime containerRuntime,
        INetworkingServiceFactory networkingServiceFactory,
        IGitService gitService,
        IDeploymentLogService logService,
        IServiceTemplateNamespaceProvider templateNamespaceProvider,
        IRepositoryLockProvider repositoryLocks)
    {
        _repositoryLocks = repositoryLocks;
        _logger = logger;
        _dockerClient = dockerClient;
        _containerRuntime = containerRuntime;
        _gitService = gitService;
        _logService = logService;
        _templateNamespaceProvider = templateNamespaceProvider;
        _networkingService = networkingServiceFactory.Create(ServiceType.DockerImage) ?? throw new InvalidOperationException("No networking service found for Docker networking");
    }

    public bool CanHandle(IDeployableContainer container) =>
        container is Service { Type: ServiceType.Dockerfile } service && service.SourceConfig is DockerfileConfig;

    public async Task<Result<DeployData>> DeployAsync(IDeployableContainer container, Guid? deploymentId, CancellationToken cancellationToken)
    {
        if (container is not Service service) return Error.NotSupported;
        if (deploymentId is not { } depId) return Error.Failed;

        var environment = service.Environment;
        if (environment == null) return Error.NotFoundFor(nameof(Environment), service.EnvironmentId);
        var project = environment.Project;
        if (project == null) return Error.NotFoundFor(nameof(Project), environment.ProjectId);

        var dockerfileConfig = service.SourceConfig as DockerfileConfig;
        if (dockerfileConfig == null || string.IsNullOrWhiteSpace(dockerfileConfig.Content) && dockerfileConfig.Source == DockerfileSource.Raw)
        {
            if (dockerfileConfig?.Source == DockerfileSource.Git && string.IsNullOrWhiteSpace(dockerfileConfig.Repository))
                return Error.Git.RepositoryNotFound;
            if (dockerfileConfig == null)
                return Error.InvalidSourceConfig;
        }

        var imageTag = DockerUtils.BuildImageTag(service.Environment?.Project?.Alias, service.Environment?.Alias, service.Alias, service.Id);

        _logger.LogInformation(
            "Building Docker image '{ImageTag}' for service '{ServiceName}' from project '{ProjectName}'",
            imageTag,
            service.Name,
            project.Name);

        try
        {
            await _dockerClient.Images.DeleteImageAsync(imageTag, new ImageDeleteParameters { Force = true }, cancellationToken);
        }
        catch
        {
            _logger.LogDebug("Could not remove old image '{ImageTag}', proceeding with build", imageTag);
        }

        Stream buildContext;
        string dockerfilePath;

        if (dockerfileConfig.Source == DockerfileSource.Git)
        {
            var binding = dockerfileConfig.ToRepositoryBinding()!;
            var repoExists = _gitService.RepositoryExists(binding);
            await _logService.AppendLogAsync(depId, repoExists
                ? $"Pulling latest changes from '{dockerfileConfig.Branch ?? "main"}'..."
                : $"Cloning repository '{dockerfileConfig.Repository}'...", cancellationToken);

            var syncResult = await _gitService.SyncRepositoryAsync(binding, cancellationToken);
            if (syncResult.IsFailure)
            {
                if (!repoExists)
                {
                    _logger.LogError("Failed to clone repository '{Repository}' for service '{ServiceName}'", dockerfileConfig.Repository, service.Name);
                    await _logService.AppendLogAsync(depId, $"Failed to clone repository '{dockerfileConfig.Repository}'.", cancellationToken);
                    return syncResult.Error;
                }

                _logger.LogWarning("Failed to pull latest changes for service '{ServiceName}', proceeding with existing code", service.Name);
                await _logService.AppendLogAsync(depId, "Failed to pull latest changes, proceeding with existing code.", cancellationToken);
            }
            else
            {
                await _logService.AppendLogAsync(depId, repoExists ? "Repository updated successfully." : "Repository cloned successfully.", cancellationToken);
            }

            var repoPath = _gitService.GetRepositoryPath(binding);
            if (string.IsNullOrWhiteSpace(repoPath))
                return Error.Git.RepositoryNotFound;

            var repoFullPath = Path.GetFullPath(repoPath);
            var contextFullPath = repoFullPath;

            if (!string.IsNullOrWhiteSpace(dockerfileConfig.BuildContext))
            {
                contextFullPath = Path.GetFullPath(Path.Combine(repoFullPath, dockerfileConfig.BuildContext));
                if (!contextFullPath.StartsWith(repoFullPath, StringComparison.Ordinal))
                    return Error.InvalidSourceConfig;
            }

            var dockerfileFullPath = Path.GetFullPath(Path.Combine(repoFullPath, dockerfileConfig.FilePath ?? "Dockerfile"));
            dockerfilePath = Path.GetRelativePath(contextFullPath, dockerfileFullPath).Replace('\\', '/');

            // The clone is shared with other services, so snapshot it while no one else is pulling into it.
            using (await _repositoryLocks.AcquireAsync(repoFullPath, cancellationToken))
                buildContext = await DockerUtils.CreateTarArchiveFromDirectoryAsync(contextFullPath, cancellationToken);
        }
        else
        {
            buildContext = await DockerUtils.CreateTarArchiveFromContentAsync(dockerfileConfig.Content!, cancellationToken);
            dockerfilePath = "Dockerfile";
        }

        using (buildContext)
        {
            var buildArgs = dockerfileConfig.BuildArgs ?? new Dictionary<string, string>();

            if (buildArgs.Count > 0)
            {
                var namespaces = await _templateNamespaceProvider.BuildAsync(service.Id, cancellationToken);
                if (namespaces == null)
                    return Error.NotFoundFor(nameof(Service), service.Id);

                var resolvedBuildArgs = new Dictionary<string, string>();
                foreach (var (key, value) in buildArgs)
                {
                    var resolvedValue = TemplateExpressionResolver.Resolve(value, namespaces);
                    resolvedBuildArgs[key] = resolvedValue;
                }
                buildArgs = resolvedBuildArgs;
            }

            var buildParams = new ImageBuildParameters
            {
                Tags = [imageTag],
                Dockerfile = dockerfilePath,
                Labels = DockerUtils.BuildContainerLabels(service),
                BuildArgs = buildArgs
            };

            _logger.LogInformation("Starting docker build for image '{ImageTag}'", imageTag);
            await _logService.AppendLogAsync(depId, $"Building image '{imageTag}'...", cancellationToken);

            var buildResult = await WaitForImageBuildAsync(
                _dockerClient,
                buildParams,
                buildContext,
                imageTag,
                depId,
                cancellationToken);

            if (buildResult.IsFailure)
                return buildResult.Error;
        }

        await _logService.AppendLogAsync(depId, "Image built successfully. Creating container...", cancellationToken);

        // Only stop the old container now that the new image is fully built, to minimize downtime
        // and avoid marking the service as stopped before the replacement is ready to start.
        await _containerRuntime.RemoveAllForOwnerAsync(service.Id, _networkingService, "removed before redeploying", cancellationToken);

        _logger.LogInformation(
            "Deploying service '{ServiceName}' from project '{ProjectName}' from Dockerfile",
            service.Name,
            project.Name);

        var (param, environmentNetworkName) = await _containerRuntime.BuildServiceContainerParametersAsync(
            service, imageTag, dockerfileConfig.Ports, dockerfileConfig.CommandArgs, dockerfileConfig.RestartPolicy,
            ensureNamedVolumesReady: false, _networkingService, cancellationToken);

        Result<string> createResult;
        try
        {
            createResult = await _containerRuntime.CreateAndStartAsync(param, cancellationToken);
        }
        catch (DockerApiException ex)
        {
            _logger.LogError(ex, "Failed to create/start Docker container for service '{ServiceName}': {StatusCode} {Message}",
                service.Name, ex.StatusCode, ex.Message);
            await _logService.AppendLogAsync(depId, $"Failed to create/start container: {ex.Message}", cancellationToken);
            return Error.Docker.FailedToStartContainer;
        }

        if (createResult.IsFailure)
        {
            await _logService.AppendLogAsync(depId, "Failed to start container.", cancellationToken);
            return createResult.Error;
        }

        await _containerRuntime.ConnectServiceToAssignedNetworksAsync(service, _networkingService, cancellationToken);

        await _logService.AppendLogAsync(depId, "Container started successfully.", cancellationToken);

        _logger.LogInformation(
            "Successfully deployed service '{ServiceName}' from project '{ProjectName}' from Dockerfile",
            service.Name,
            project.Name);

        var inspect = await _dockerClient.Containers.InspectContainerAsync(createResult.Value, cancellationToken);
        var deployData = _containerRuntime.BuildServiceDeployData(service, param.Name, inspect, environmentNetworkName);

        await _containerRuntime.HealTraefikRoutingBestEffortAsync(service.Id, deployData.IpAddress?.ToString(), cancellationToken);

        return deployData;
    }

    public async Task<Result> StopAsync(IDeployableContainer container, CancellationToken cancellationToken)
    {
        if (container is not Service service) return Error.NotSupported;

        var containers = await _containerRuntime.GetContainersByLabelAsync(DockerUtils.BuildIdLabel(service.Id), cancellationToken);

        if (containers.Count == 0)
        {
            // Nothing to stop: a crash-looping container is often gone between restarts. Stopping is idempotent, so this is success.
            _logger.LogInformation("No Docker container found for service '{ServiceName}'; already stopped", service.Name);
            return Result.Success();
        }

        await _containerRuntime.StopAndRemoveAsync((IReadOnlyCollection<ContainerListResponse>)containers, service.Id, _networkingService,
            "stopped and removed", cancellationToken);

        return Result.Success();
    }

    public async Task<Result<DeployData>> StartAsync(IDeployableContainer container, CancellationToken cancellationToken)
    {
        if (container is not Service service) return Error.NotSupported;

        var environment = service.Environment;
        if (environment == null) return Error.NotFoundFor(nameof(Environment), service.EnvironmentId);
        var project = environment.Project;
        if (project == null) return Error.NotFoundFor(nameof(Project), environment.ProjectId);

        var dockerfileConfig = service.SourceConfig as DockerfileConfig;
        if (dockerfileConfig == null)
            return Error.InvalidSourceConfig;

        var imageTag = DockerUtils.BuildImageTag(service.Environment?.Project?.Alias, service.Environment?.Alias, service.Alias, service.Id);

        _logger.LogInformation(
            "Starting service '{ServiceName}' from project '{ProjectName}'",
            service.Name,
            project.Name);

        var (param, environmentNetworkName) = await _containerRuntime.BuildServiceContainerParametersAsync(
            service, imageTag, dockerfileConfig.Ports, dockerfileConfig.CommandArgs, dockerfileConfig.RestartPolicy,
            ensureNamedVolumesReady: false, _networkingService, cancellationToken);

        Result<string> createResult;
        try
        {
            createResult = await _containerRuntime.CreateAndStartAsync(param, cancellationToken);
        }
        catch (DockerApiException ex)
        {
            _logger.LogError(ex, "Failed to create/start Docker container for service '{ServiceName}': {StatusCode} {Message}",
                service.Name, ex.StatusCode, ex.Message);
            return Error.Docker.FailedToStartContainer;
        }

        if (createResult.IsFailure)
            return createResult.Error;

        await _containerRuntime.ConnectServiceToAssignedNetworksAsync(service, _networkingService, cancellationToken);

        _logger.LogInformation(
            "Successfully started service '{ServiceName}' from project '{ProjectName}'",
            service.Name,
            project.Name);

        var inspect = await _dockerClient.Containers.InspectContainerAsync(createResult.Value, cancellationToken);
        var deployData = _containerRuntime.BuildServiceDeployData(service, param.Name, inspect, environmentNetworkName);

        await _containerRuntime.HealTraefikRoutingBestEffortAsync(service.Id, deployData.IpAddress?.ToString(), cancellationToken);

        return deployData;
    }

    public async Task CleanupAsync(IDeployableContainer container, CancellationToken cancellationToken)
    {
        if (container is not Service service) return;

        await _containerRuntime.RemoveAllForOwnerAsync(service.Id, _networkingService, "cleaned up for deleted service", cancellationToken);

        var imageTag = DockerUtils.BuildImageTag(service.Environment?.Project?.Alias, service.Environment?.Alias, service.Alias, service.Id);
        try
        {
            await _dockerClient.Images.DeleteImageAsync(imageTag, new ImageDeleteParameters { Force = true }, cancellationToken);
            _logger.LogInformation("Removed built image '{ImageTag}' for deleted service '{ServiceName}'", imageTag, service.Name);
        }
        catch
        {
            _logger.LogDebug("Could not remove image '{ImageTag}' for deleted service '{ServiceName}', it may not exist", imageTag, service.Name);
        }
    }

    private async Task<Result> WaitForImageBuildAsync(
        IDockerClient dockerClient,
        ImageBuildParameters buildParams,
        Stream buildContext,
        string imageTag,
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        var buildErrors = new List<string>();

        var progress = new Progress<JSONMessage>(message =>
        {
            if (message.Stream != null)
            {
                var line = message.Stream.TrimEnd();
                if (!string.IsNullOrEmpty(line))
                {
                    _logger.LogDebug("Docker build output: {Output}", line);
                    _ = _logService.AppendLogAsync(deploymentId, line, cancellationToken);
                }
            }

            if (message.Error != null)
            {
                _logger.LogError("Docker build error: {Error}", message.ErrorMessage);
                buildErrors.Add(message.ErrorMessage);
                _ = _logService.AppendLogAsync(deploymentId, $"ERROR: {message.ErrorMessage}", cancellationToken);
            }
        });

        try
        {
            await dockerClient.Images.BuildImageFromDockerfileAsync(
                buildParams,
                buildContext,
                null,
                null,
                progress,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Docker build failed for image '{ImageTag}'", imageTag);
            await _logService.AppendLogAsync(deploymentId, $"Docker build failed: {ex.Message}", cancellationToken);
            return Error.Docker.BuildFailed;
        }

        if (buildErrors.Count > 0)
        {
            _logger.LogError("Docker build failed for image '{ImageTag}' with errors: {Errors}",
                imageTag, string.Join("; ", buildErrors));
            return Error.Docker.BuildFailed;
        }

        _logger.LogInformation("Docker image '{ImageTag}' built successfully", imageTag);
        return Result.Success();
    }
}