using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.ServiceBroker.Core.Interfaces;

/// <summary>
/// Persistence-layer abstraction over service instance state. Implementations are responsible
/// for atomicity (a single SaveChanges per call) and indexing on <c>InstanceId</c>.
/// </summary>
public interface IServiceInstanceRepository
{
    Task<ServiceInstance?> GetByIdAsync(string instanceId, CancellationToken ct = default);

    Task<ServiceInstance> CreateAsync(ServiceInstance instance, CancellationToken ct = default);

    Task UpdateStateAsync(string instanceId, ServiceInstanceState state, string? description = null, CancellationToken ct = default);

    Task DeleteAsync(string instanceId, CancellationToken ct = default);
}

/// <summary>Domain-layer view of a service instance, independent of EF entity types.</summary>
public sealed record ServiceInstance
{
    public required string InstanceId { get; init; }
    public required string ServiceId { get; init; }
    public required string PlanId { get; init; }
    public required ServiceInstanceState State { get; init; }
    public required ProvisionParameters Parameters { get; init; }
    public string? LastOperationDescription { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
