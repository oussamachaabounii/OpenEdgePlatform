using OpenEdgePlatform.ProxyConfig.Core.Models;

namespace OpenEdgePlatform.ControlPlane.Core.Models;

/// <summary>A connected Envoy proxy known to the control plane via an active ADS stream.</summary>
public sealed record ProxyNode
{
    public required string NodeId { get; init; }
    public required string Cluster { get; init; }
    public required string Region { get; init; }
    public required string Zone { get; init; }
    public required DateTimeOffset ConnectedAt { get; init; }
    public string? CurrentVersion { get; set; }
}

/// <summary>Strongly typed configuration bound from <c>appsettings:ControlPlane</c>.</summary>
public sealed class ControlPlaneOptions
{
    public const string SectionName = "ControlPlane";
    public int GrpcPort { get; init; } = 18000;
    public int RestPort { get; init; } = 8081;
    public TimeSpan SnapshotCacheTtl { get; init; } = TimeSpan.FromMinutes(5);
    public bool EnableRedisCache { get; init; } = true;
}

/// <summary>Aggregated view returned by the REST <c>/proxies</c> endpoint.</summary>
public sealed record ProxyNodeSummary(string NodeId, string Region, string Zone, string? Version, DateTimeOffset ConnectedAt);

/// <summary>Persisted record of an xDS snapshot, indexed by instance id.</summary>
public sealed record StoredSnapshot(string InstanceId, string Version, XdsSnapshot Snapshot, DateTimeOffset CreatedAt);
