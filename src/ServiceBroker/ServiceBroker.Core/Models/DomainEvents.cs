using OpenEdgePlatform.ProxyConfig.Core.Builders;

namespace OpenEdgePlatform.ServiceBroker.Core.Models;

/// <summary>Marker interface for events flowing through the async messaging bus.</summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

/// <summary>Emitted by the broker when a new provision request is accepted. Consumed by the provisioning worker.</summary>
public sealed record ProvisioningRequestedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public required string InstanceId { get; init; }
    public required string ServiceId { get; init; }
    public required string PlanId { get; init; }
    public required string UpstreamService { get; init; }
    public int UpstreamPort { get; init; }
    public required string Hostname { get; init; }
    public int ListenerPort { get; init; }
    public IReadOnlyList<string>? PreferredRegions { get; init; }
}

/// <summary>Emitted by the worker after a successful provisioning. Consumed by the control plane.</summary>
public sealed record ProvisioningCompletedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public required string InstanceId { get; init; }
    public required EdgeResourceAllocation Allocation { get; init; }
}

/// <summary>Emitted by the worker when provisioning fails. The broker updates instance state on consumption.</summary>
public sealed record ProvisioningFailedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public required string InstanceId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>Emitted by the broker when a deprovision request is accepted.</summary>
public sealed record DeprovisioningRequestedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public required string InstanceId { get; init; }
    public required string ServiceId { get; init; }
    public required string PlanId { get; init; }
}

/// <summary>
/// Worker's verdict on how an instance should be exposed across the proxy fleet. The control plane
/// translates this into the corresponding xDS resources for the affected regions.
/// </summary>
public sealed record EdgeResourceAllocation
{
    public required string InstanceId { get; init; }
    public required string ListenerName { get; init; }
    public required string ClusterName { get; init; }
    public required string RouteName { get; init; }
    public required string VirtualHostName { get; init; }
    public required string Hostname { get; init; }
    public required int ListenerPort { get; init; }
    public required IReadOnlyList<string> Regions { get; init; }
    public required IReadOnlyList<UpstreamEndpoint> Endpoints { get; init; }
}

/// <summary>Stable RabbitMQ exchange / Service Bus topic names used across services.</summary>
public static class MessagingTopics
{
    public const string ProvisioningRequested = "provisioning.requested";
    public const string ProvisioningCompleted = "provisioning.completed";
    public const string ProvisioningFailed = "provisioning.failed";
    public const string DeprovisioningRequested = "deprovisioning.requested";
}
