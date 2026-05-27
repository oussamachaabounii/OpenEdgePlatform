using System.Text.Json;
using OpenEdgePlatform.ProxyConfig.Core.Models;

namespace OpenEdgePlatform.ProxyConfig.Infrastructure.Serialization;

/// <summary>
/// Serializes <see cref="XdsSnapshot"/> instances to the JSON shape Envoy accepts in
/// static <c>resources</c> blocks and in <c>DiscoveryResponse</c> bodies.
/// </summary>
public static class XdsJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static string Serialize(XdsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Validate(snapshot);
        return JsonSerializer.Serialize(snapshot, Options);
    }

    public static string SerializeResource<T>(T resource) where T : class
    {
        ArgumentNullException.ThrowIfNull(resource);
        return JsonSerializer.Serialize(resource, Options);
    }

    /// <summary>
    /// Throws <see cref="InvalidXdsSnapshotException"/> if any required field is missing.
    /// Cheap structural validation — does not duplicate the cross-resource checks performed
    /// by <see cref="Core.Builders.XdsSnapshotBuilder"/>.
    /// </summary>
    public static void Validate(XdsSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Version))
        {
            throw new InvalidXdsSnapshotException("Snapshot version is required.");
        }

        foreach (var listener in snapshot.Listeners)
        {
            if (string.IsNullOrWhiteSpace(listener.Name))
            {
                throw new InvalidXdsSnapshotException("Listener.name is required.");
            }
            if (string.IsNullOrWhiteSpace(listener.Address.SocketAddress.Address) ||
                listener.Address.SocketAddress.PortValue <= 0)
            {
                throw new InvalidXdsSnapshotException($"Listener '{listener.Name}' has invalid socket address.");
            }
        }

        foreach (var cluster in snapshot.Clusters)
        {
            if (string.IsNullOrWhiteSpace(cluster.Name))
            {
                throw new InvalidXdsSnapshotException("Cluster.name is required.");
            }
        }

        foreach (var route in snapshot.Routes)
        {
            if (string.IsNullOrWhiteSpace(route.Name))
            {
                throw new InvalidXdsSnapshotException("RouteConfiguration.name is required.");
            }
        }

        foreach (var ep in snapshot.Endpoints)
        {
            if (string.IsNullOrWhiteSpace(ep.ClusterName))
            {
                throw new InvalidXdsSnapshotException("ClusterLoadAssignment.cluster_name is required.");
            }
        }
    }
}
