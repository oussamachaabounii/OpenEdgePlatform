using Microsoft.Extensions.Logging;
using OpenEdgePlatform.ControlPlane.Core.Interfaces;
using OpenEdgePlatform.ProxyConfig.Core.Builders;
using OpenEdgePlatform.ProxyConfig.Core.Models;
using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.ControlPlane.Core.Services;

public sealed class XdsConfigGeneratorService : IXdsConfigGeneratorService
{
    private readonly ILogger<XdsConfigGeneratorService> _logger;

    public XdsConfigGeneratorService(ILogger<XdsConfigGeneratorService> logger) => _logger = logger;

    public XdsSnapshot GenerateSnapshot(EdgeResourceAllocation allocation, string version)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var snapshot = new XdsSnapshotBuilder()
            .ForInstance(allocation.InstanceId)
            .WithListener(allocation.ListenerName, allocation.ListenerPort, allocation.RouteName)
            .WithCluster(allocation.ClusterName)
            .WithRoute(
                allocation.RouteName,
                allocation.VirtualHostName,
                new[] { allocation.Hostname },
                allocation.ClusterName)
            .WithEndpoints(allocation.ClusterName, allocation.Endpoints)
            .Build(version);

        _logger.LogInformation(
            "Generated snapshot {Version} for instance {InstanceId}: {ListenerCount} listener(s), {ClusterCount} cluster(s), {EndpointCount} endpoint group(s).",
            version,
            allocation.InstanceId,
            snapshot.Listeners.Count,
            snapshot.Clusters.Count,
            snapshot.Endpoints.Sum(e => e.Endpoints.Count));

        return snapshot;
    }
}
