using OpenEdgePlatform.ControlPlane.Core.Models;
using OpenEdgePlatform.ProxyConfig.Core.Models;

namespace OpenEdgePlatform.ControlPlane.Core.Interfaces;

/// <summary>
/// Pushes a new snapshot to connected Envoy proxies whose locality intersects with the snapshot regions.
/// Backed by the live ADS gRPC server; injected as an abstraction so the messaging consumer can stay
/// free of gRPC types.
/// </summary>
public interface IXdsSnapshotPublisher
{
    /// <summary>Pushes the given snapshot to every connected proxy in <paramref name="regions"/>.</summary>
    Task PushAsync(XdsSnapshot snapshot, IReadOnlyList<string> regions, CancellationToken ct = default);

    /// <summary>Returns a snapshot of currently connected proxy nodes.</summary>
    IReadOnlyList<ProxyNodeSummary> ListProxies();
}
