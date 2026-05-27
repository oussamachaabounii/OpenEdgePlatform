using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.ServiceBroker.Core.Interfaces;

/// <summary>
/// Application-layer service that orchestrates the OSB lifecycle. Owns state-machine
/// transitions, idempotency checks, and downstream event publishing.
/// </summary>
public interface IServiceInstanceService
{
    Task<ProvisionResult> ProvisionAsync(string instanceId, ProvisionRequest request, CancellationToken ct = default);

    Task<ProvisionResult> DeprovisionAsync(string instanceId, string serviceId, string planId, CancellationToken ct = default);

    Task<ServiceInstance?> GetAsync(string instanceId, CancellationToken ct = default);

    Task<LastOperationResponse?> GetLastOperationAsync(string instanceId, CancellationToken ct = default);

    /// <summary>Reconciler hook used by worker consumers to update instance state.</summary>
    Task ApplyTerminalStateAsync(string instanceId, ServiceInstanceState state, string? description, CancellationToken ct = default);
}
