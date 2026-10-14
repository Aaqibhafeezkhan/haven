using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Messaging;

namespace Haven.Application.Features.Networks.Commands.ReconcileNetworks;

/// <summary>
/// Runs the network drift repair on demand. Defaults to a dry run so nothing changes unless the
/// caller explicitly asks for it.
/// </summary>
[RequirePermission(Permissions.Dns.ManageNetworks)]
public sealed record ReconcileNetworksCommand(bool DryRun = true) : ICommand<NetworkRepairReport>, IMutatesManifestState;