using System.Text.Json.Serialization;

namespace OpenEdgePlatform.ProxyConfig.Core.Models;

/// <summary>
/// xDS v3 <c>envoy.config.endpoint.v3.ClusterLoadAssignment</c>.
/// Maps a cluster to a set of endpoints, grouped by locality.
/// </summary>
public sealed record XdsClusterLoadAssignment
{
    [JsonPropertyName("@type")]
    public string TypeUrl { get; init; } = "type.googleapis.com/envoy.config.endpoint.v3.ClusterLoadAssignment";

    [JsonPropertyName("cluster_name")]
    public required string ClusterName { get; init; }

    [JsonPropertyName("endpoints")]
    public IReadOnlyList<LocalityLbEndpoints> Endpoints { get; init; } = [];
}

/// <summary>Set of endpoints sharing a locality (region/zone).</summary>
public sealed record LocalityLbEndpoints
{
    [JsonPropertyName("locality")]
    public Locality Locality { get; init; } = new();

    [JsonPropertyName("lb_endpoints")]
    public IReadOnlyList<LbEndpoint> LbEndpoints { get; init; } = [];

    [JsonPropertyName("load_balancing_weight")]
    public int? LoadBalancingWeight { get; init; }

    [JsonPropertyName("priority")]
    public int Priority { get; init; }
}

public sealed record Locality
{
    [JsonPropertyName("region")]
    public string Region { get; init; } = string.Empty;

    [JsonPropertyName("zone")]
    public string Zone { get; init; } = string.Empty;

    [JsonPropertyName("sub_zone")]
    public string SubZone { get; init; } = string.Empty;
}

public sealed record LbEndpoint
{
    [JsonPropertyName("endpoint")]
    public Endpoint Endpoint { get; init; } = new();

    [JsonPropertyName("health_status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HealthStatus HealthStatus { get; init; } = HealthStatus.HEALTHY;

    [JsonPropertyName("load_balancing_weight")]
    public int? LoadBalancingWeight { get; init; }
}

public sealed record Endpoint
{
    [JsonPropertyName("address")]
    public XdsAddress Address { get; init; } = new();

    [JsonPropertyName("hostname")]
    public string? Hostname { get; init; }
}

public sealed record XdsAddress
{
    [JsonPropertyName("socket_address")]
    public SocketAddress SocketAddress { get; init; } = new();
}

public sealed record SocketAddress
{
    [JsonPropertyName("address")]
    public string Address { get; init; } = string.Empty;

    [JsonPropertyName("port_value")]
    public int PortValue { get; init; }

    [JsonPropertyName("protocol")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SocketProtocol Protocol { get; init; } = SocketProtocol.TCP;
}

public enum SocketProtocol
{
    TCP,
    UDP
}

public enum HealthStatus
{
    UNKNOWN,
    HEALTHY,
    UNHEALTHY,
    DRAINING,
    TIMEOUT,
    DEGRADED
}
