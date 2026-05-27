using OpenEdgePlatform.Provisioning.Core.Interfaces;
using OpenEdgePlatform.ServiceBroker.Core.Interfaces;
using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.Provisioning.Core.Services;

/// <summary>
/// Default <see cref="IInstanceStatusUpdater"/> that writes directly to the broker's persistence layer.
/// Lives in core so worker hosts can wire it without taking a hard dependency on the API project.
/// </summary>
public sealed class InstanceStatusUpdater : IInstanceStatusUpdater
{
    private readonly IServiceInstanceRepository _repository;

    public InstanceStatusUpdater(IServiceInstanceRepository repository) => _repository = repository;

    public Task SetProvisioningAsync(string instanceId, CancellationToken ct = default) =>
        _repository.UpdateStateAsync(instanceId, ServiceInstanceState.Provisioning, "Worker picked up request.", ct);

    public Task SetProvisionedAsync(string instanceId, string description, CancellationToken ct = default) =>
        _repository.UpdateStateAsync(instanceId, ServiceInstanceState.Provisioned, description, ct);

    public Task SetFailedAsync(string instanceId, string reason, CancellationToken ct = default) =>
        _repository.UpdateStateAsync(instanceId, ServiceInstanceState.Failed, reason, ct);

    public Task SetDeprovisionedAsync(string instanceId, CancellationToken ct = default) =>
        _repository.DeleteAsync(instanceId, ct);
}
