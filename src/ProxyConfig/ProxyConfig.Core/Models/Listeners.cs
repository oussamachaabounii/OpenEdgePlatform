using System.Text.Json.Serialization;

namespace OpenEdgePlatform.ProxyConfig.Core.Models;

/// <summary>
/// xDS v3 <c>envoy.config.listener.v3.Listener</c>.
/// An ingress socket plus the filter chain that processes traffic on it.
/// </summary>
public sealed record XdsListener
{
    [JsonPropertyName("@type")]
    public string TypeUrl { get; init; } = "type.googleapis.com/envoy.config.listener.v3.Listener";

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("address")]
    public XdsAddress Address { get; init; } = new();

    [JsonPropertyName("filter_chains")]
    public IReadOnlyList<FilterChain> FilterChains { get; init; } = [];
}

public sealed record FilterChain
{
    [JsonPropertyName("filters")]
    public IReadOnlyList<XdsFilter> Filters { get; init; } = [];

    [JsonPropertyName("filter_chain_match")]
    public FilterChainMatch? FilterChainMatch { get; init; }
}

public sealed record FilterChainMatch
{
    [JsonPropertyName("server_names")]
    public IReadOnlyList<string>? ServerNames { get; init; }

    [JsonPropertyName("transport_protocol")]
    public string? TransportProtocol { get; init; }
}

public sealed record XdsFilter
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("typed_config")]
    public TypedConfig? TypedConfig { get; init; }
}

/// <summary>
/// Polymorphic typed configuration. Holds either an inline route config or an RDS reference
/// for an <see cref="HttpConnectionManager"/>.
/// </summary>
public sealed record TypedConfig
{
    [JsonPropertyName("@type")]
    public required string TypeUrl { get; init; }

    [JsonPropertyName("stat_prefix")]
    public string? StatPrefix { get; init; }

    [JsonPropertyName("codec_type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CodecType? CodecType { get; init; }

    [JsonPropertyName("rds")]
    public RdsConfig? Rds { get; init; }

    [JsonPropertyName("route_config")]
    public XdsRouteConfiguration? RouteConfig { get; init; }

    [JsonPropertyName("http_filters")]
    public IReadOnlyList<HttpFilter>? HttpFilters { get; init; }

    [JsonPropertyName("access_log")]
    public IReadOnlyList<AccessLog>? AccessLog { get; init; }
}

public sealed record RdsConfig
{
    [JsonPropertyName("config_source")]
    public ConfigSource ConfigSource { get; init; } = new();

    [JsonPropertyName("route_config_name")]
    public required string RouteConfigName { get; init; }
}

public sealed record HttpFilter
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("typed_config")]
    public object? TypedConfig { get; init; }
}

public sealed record AccessLog
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "envoy.access_loggers.stdout";

    [JsonPropertyName("typed_config")]
    public object? TypedConfig { get; init; }
}

public enum CodecType
{
    AUTO,
    HTTP1,
    HTTP2,
    HTTP3
}

/// <summary>Well-known xDS type URLs used in <see cref="TypedConfig.TypeUrl"/>.</summary>
public static class XdsTypeUrls
{
    public const string HttpConnectionManager =
        "type.googleapis.com/envoy.extensions.filters.network.http_connection_manager.v3.HttpConnectionManager";
    public const string Router =
        "type.googleapis.com/envoy.extensions.filters.http.router.v3.Router";
    public const string Listener =
        "type.googleapis.com/envoy.config.listener.v3.Listener";
    public const string Cluster =
        "type.googleapis.com/envoy.config.cluster.v3.Cluster";
    public const string RouteConfiguration =
        "type.googleapis.com/envoy.config.route.v3.RouteConfiguration";
    public const string ClusterLoadAssignment =
        "type.googleapis.com/envoy.config.endpoint.v3.ClusterLoadAssignment";
}
