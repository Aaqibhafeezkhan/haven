using Haven.Application.Common;
using Haven.Application.Common.Interfaces.Deployment;
using Haven.Application.Common.Messaging;

namespace Haven.Application.Features.Networks.Queries.GetNetworkHealth;

[RequirePermission(Permissions.Dns.Read)]
public sealed class GetNetworkHealthQuery : IQuery<NetworkHealthReport>;
