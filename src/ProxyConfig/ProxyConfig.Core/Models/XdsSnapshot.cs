using System.Text.Json.Serialization;

namespace OpenEdgePlatform.ProxyConfig.Core.Models;

/// <summary>
/// A versioned bundle of all xDS resources that should be sent to a single Envoy proxy.
/// One snapshot per (node, version). Versions are monotonically increasing strings —
/// the control plane uses unix-ms timestamps.
/// </summary>
public sealed record XdsSnapshot
{
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("listeners")]
    public IReadOnlyList<XdsListener> Listeners { get; init; } = [];

    [JsonPropertyName("clusters")]
    public IReadOnlyList<XdsCluster> Clusters { get; init; } = [];

    [JsonPropertyName("routes")]
    public IReadOnlyList<XdsRouteConfiguration> Routes { get; init; } = [];

    [JsonPropertyName("endpoints")]
    public IReadOnlyList<XdsClusterLoadAssignment> Endpoints { get; init; } = [];

    /// <summary>The unique identifier of the originating service instance, if any.</summary>
    [JsonPropertyName("instance_id")]
    public string? InstanceId { get; init; }

    public static XdsSnapshot Empty(string version) => new() { Version = version };
}

/// <summary>
/// Thrown by serializers and consumers when a snapshot is missing required fields or
/// references resources that do not exist within it.
/// </summary>
public sealed class InvalidXdsSnapshotException : Exception
{
    public InvalidXdsSnapshotException(string message) : base(message) { }
    public InvalidXdsSnapshotException(string message, Exception inner) : base(message, inner) { }
}
