using System.Text.Json.Serialization;

namespace OpenEdgePlatform.ProxyConfig.Core.Models;

/// <summary>
/// xDS v3 <c>envoy.config.route.v3.RouteConfiguration</c>.
/// Holds virtual hosts and their routes for an HCM listener.
/// </summary>
public sealed record XdsRouteConfiguration
{
    [JsonPropertyName("@type")]
    public string TypeUrl { get; init; } = "type.googleapis.com/envoy.config.route.v3.RouteConfiguration";

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("virtual_hosts")]
    public IReadOnlyList<VirtualHost> VirtualHosts { get; init; } = [];
}

public sealed record VirtualHost
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("domains")]
    public IReadOnlyList<string> Domains { get; init; } = [];

    [JsonPropertyName("routes")]
    public IReadOnlyList<Route> Routes { get; init; } = [];
}

public sealed record Route
{
    [JsonPropertyName("match")]
    public RouteMatch Match { get; init; } = new();

    [JsonPropertyName("route")]
    public RouteAction? RouteAction { get; init; }
}

public sealed record RouteMatch
{
    [JsonPropertyName("prefix")]
    public string? Prefix { get; init; }

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("safe_regex")]
    public RegexMatcher? SafeRegex { get; init; }
}

public sealed record RegexMatcher
{
    [JsonPropertyName("regex")]
    public required string Regex { get; init; }
}

public sealed record RouteAction
{
    [JsonPropertyName("cluster")]
    public string? Cluster { get; init; }

    [JsonPropertyName("weighted_clusters")]
    public WeightedClusters? WeightedClusters { get; init; }

    [JsonPropertyName("timeout")]
    public string Timeout { get; init; } = "15s";

    [JsonPropertyName("retry_policy")]
    public RetryPolicy? RetryPolicy { get; init; }
}

public sealed record WeightedClusters
{
    [JsonPropertyName("clusters")]
    public IReadOnlyList<WeightedClusterEntry> Clusters { get; init; } = [];
}

public sealed record WeightedClusterEntry
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("weight")]
    public int Weight { get; init; }
}

public sealed record RetryPolicy
{
    [JsonPropertyName("retry_on")]
    public string RetryOn { get; init; } = "5xx,reset,connect-failure";

    [JsonPropertyName("num_retries")]
    public int NumRetries { get; init; } = 3;

    [JsonPropertyName("per_try_timeout")]
    public string PerTryTimeout { get; init; } = "5s";
}
