using System.Text.Json.Serialization;

namespace OpenEdgePlatform.ServiceBroker.Core.Models;

/// <summary>
/// Open Service Broker (OSB) v2.17 provision request body — see osb-api section 5.2.
/// </summary>
public sealed record ProvisionRequest
{
    [JsonPropertyName("service_id")]
    public required string ServiceId { get; init; }

    [JsonPropertyName("plan_id")]
    public required string PlanId { get; init; }

    [JsonPropertyName("organization_guid")]
    public string? OrganizationGuid { get; init; }

    [JsonPropertyName("space_guid")]
    public string? SpaceGuid { get; init; }

    [JsonPropertyName("parameters")]
    public ProvisionParameters? Parameters { get; init; }

    [JsonPropertyName("context")]
    public Dictionary<string, object>? Context { get; init; }
}

/// <summary>Platform-specific parameters for our edge load-balancer service.</summary>
public sealed record ProvisionParameters
{
    /// <summary>The upstream service name (Kubernetes service or DNS name) traffic is routed to.</summary>
    [JsonPropertyName("upstream_service")]
    public required string UpstreamService { get; init; }

    /// <summary>Port the upstream service listens on.</summary>
    [JsonPropertyName("upstream_port")]
    public int UpstreamPort { get; init; } = 8080;

    /// <summary>The hostname (Host header) to match incoming requests against.</summary>
    [JsonPropertyName("hostname")]
    public required string Hostname { get; init; }

    /// <summary>Listener port on the edge proxies. Defaults to 443.</summary>
    [JsonPropertyName("listener_port")]
    public int ListenerPort { get; init; } = 443;

    /// <summary>Optional explicit list of regions; defaults to all configured regions.</summary>
    [JsonPropertyName("regions")]
    public IReadOnlyList<string>? Regions { get; init; }
}

public sealed record ProvisionResponse
{
    [JsonPropertyName("dashboard_url")]
    public string? DashboardUrl { get; init; }

    [JsonPropertyName("operation")]
    public string? Operation { get; init; }
}

public sealed record DeprovisionResponse
{
    [JsonPropertyName("operation")]
    public string? Operation { get; init; }
}

public sealed record LastOperationResponse
{
    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed record ServiceInstanceResponse
{
    [JsonPropertyName("service_id")]
    public required string ServiceId { get; init; }

    [JsonPropertyName("plan_id")]
    public required string PlanId { get; init; }

    [JsonPropertyName("dashboard_url")]
    public string? DashboardUrl { get; init; }

    [JsonPropertyName("parameters")]
    public ProvisionParameters? Parameters { get; init; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; init; }
}

public sealed record OsbErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("instance_usable")]
    public bool? InstanceUsable { get; init; }
}

/// <summary>
/// State of a service instance through its lifecycle. Persisted in the database and
/// reported via the OSB <c>/last_operation</c> endpoint.
/// </summary>
public enum ServiceInstanceState
{
    Pending,
    Provisioning,
    Provisioned,
    Deprovisioning,
    Deprovisioned,
    Failed
}

/// <summary>OSB <c>state</c> field values for <c>/last_operation</c>.</summary>
public static class OsbOperationStates
{
    public const string InProgress = "in progress";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}

/// <summary>Outcome of a service-instance create or delete call from the broker's perspective.</summary>
public enum ProvisionOutcome
{
    /// <summary>A new async operation was started — return 202.</summary>
    AcceptedAsync,
    /// <summary>An identical instance already exists — return 200.</summary>
    AlreadyExistsIdentical,
    /// <summary>The instance exists with different parameters — return 409.</summary>
    Conflict,
    /// <summary>The instance does not exist (for delete) — return 410 Gone.</summary>
    Gone
}

public sealed record ProvisionResult(ProvisionOutcome Outcome, string? Operation = null, string? DashboardUrl = null);
