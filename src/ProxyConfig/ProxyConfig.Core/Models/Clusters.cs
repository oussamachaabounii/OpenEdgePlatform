using System.Text.Json.Serialization;

namespace OpenEdgePlatform.ProxyConfig.Core.Models;

/// <summary>
/// xDS v3 <c>envoy.config.cluster.v3.Cluster</c>.
/// Defines an upstream cluster targeted by routes.
/// </summary>
public sealed record XdsCluster
{
    [JsonPropertyName("@type")]
    public string TypeUrl { get; init; } = "type.googleapis.com/envoy.config.cluster.v3.Cluster";

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClusterDiscoveryType Type { get; init; } = ClusterDiscoveryType.EDS;

    [JsonPropertyName("connect_timeout")]
    public string ConnectTimeout { get; init; } = "5s";

    [JsonPropertyName("lb_policy")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LbPolicy LbPolicy { get; init; } = LbPolicy.ROUND_ROBIN;

    [JsonPropertyName("eds_cluster_config")]
    public EdsClusterConfig? EdsClusterConfig { get; init; }

    [JsonPropertyName("health_checks")]
    public IReadOnlyList<HealthCheck>? HealthChecks { get; init; }

    [JsonPropertyName("dns_lookup_family")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DnsLookupFamily DnsLookupFamily { get; init; } = DnsLookupFamily.V4_ONLY;

    [JsonPropertyName("circuit_breakers")]
    public CircuitBreakers? CircuitBreakers { get; init; }
}

public sealed record EdsClusterConfig
{
    [JsonPropertyName("eds_config")]
    public ConfigSource EdsConfig { get; init; } = new();

    [JsonPropertyName("service_name")]
    public string? ServiceName { get; init; }
}

public sealed record ConfigSource
{
    [JsonPropertyName("ads")]
    public object Ads { get; init; } = new { };

    [JsonPropertyName("resource_api_version")]
    public string ResourceApiVersion { get; init; } = "V3";
}

public sealed record HealthCheck
{
    [JsonPropertyName("timeout")]
    public string Timeout { get; init; } = "2s";

    [JsonPropertyName("interval")]
    public string Interval { get; init; } = "10s";

    [JsonPropertyName("unhealthy_threshold")]
    public int UnhealthyThreshold { get; init; } = 3;

    [JsonPropertyName("healthy_threshold")]
    public int HealthyThreshold { get; init; } = 2;

    [JsonPropertyName("http_health_check")]
    public HttpHealthCheck? HttpHealthCheck { get; init; }
}

public sealed record HttpHealthCheck
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = "/health";

    [JsonPropertyName("host")]
    public string? Host { get; init; }

    [JsonPropertyName("expected_statuses")]
    public IReadOnlyList<HttpStatusRange>? ExpectedStatuses { get; init; }
}

public sealed record HttpStatusRange
{
    [JsonPropertyName("start")]
    public int Start { get; init; } = 200;

    [JsonPropertyName("end")]
    public int End { get; init; } = 300;
}

public sealed record CircuitBreakers
{
    [JsonPropertyName("thresholds")]
    public IReadOnlyList<CircuitBreakerThreshold> Thresholds { get; init; } = [];
}

public sealed record CircuitBreakerThreshold
{
    [JsonPropertyName("priority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RoutingPriority Priority { get; init; } = RoutingPriority.DEFAULT;

    [JsonPropertyName("max_connections")]
    public int MaxConnections { get; init; } = 1024;

    [JsonPropertyName("max_pending_requests")]
    public int MaxPendingRequests { get; init; } = 1024;

    [JsonPropertyName("max_requests")]
    public int MaxRequests { get; init; } = 1024;

    [JsonPropertyName("max_retries")]
    public int MaxRetries { get; init; } = 3;
}

public enum ClusterDiscoveryType
{
    STATIC,
    STRICT_DNS,
    LOGICAL_DNS,
    EDS,
    ORIGINAL_DST
}

public enum LbPolicy
{
    ROUND_ROBIN,
    LEAST_REQUEST,
    RING_HASH,
    RANDOM,
    MAGLEV,
    CLUSTER_PROVIDED
}

public enum DnsLookupFamily
{
    AUTO,
    V4_ONLY,
    V6_ONLY,
    V4_PREFERRED,
    ALL
}

public enum RoutingPriority
{
    DEFAULT,
    HIGH
}
