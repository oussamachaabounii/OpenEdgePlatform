using Microsoft.Extensions.Logging;
using OpenEdgePlatform.ServiceBroker.Core.Interfaces;
using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.ServiceBroker.Core.Services;

public sealed class ServiceInstanceService : IServiceInstanceService
{
    private readonly IServiceInstanceRepository _repository;
    private readonly IProvisioningPublisher _publisher;
    private readonly ILogger<ServiceInstanceService> _logger;
    private readonly TimeProvider _clock;

    public ServiceInstanceService(
        IServiceInstanceRepository repository,
        IProvisioningPublisher publisher,
        ILogger<ServiceInstanceService> logger,
        TimeProvider? clock = null)
    {
        _repository = repository;
        _publisher = publisher;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ProvisionResult> ProvisionAsync(string instanceId, ProvisionRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Parameters is null)
        {
            throw new ArgumentException("Provision parameters are required.", nameof(request));
        }

        var existing = await _repository.GetByIdAsync(instanceId, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            var sameShape = string.Equals(existing.ServiceId, request.ServiceId, StringComparison.Ordinal)
                && string.Equals(existing.PlanId, request.PlanId, StringComparison.Ordinal)
                && existing.Parameters == request.Parameters;

            if (sameShape)
            {
                _logger.LogInformation("Idempotent provision for {InstanceId} — already exists with same shape.", instanceId);
                return new ProvisionResult(ProvisionOutcome.AlreadyExistsIdentical);
            }

            _logger.LogWarning("Conflict on provision for {InstanceId} — existing parameters differ.", instanceId);
            return new ProvisionResult(ProvisionOutcome.Conflict);
        }

        var now = _clock.GetUtcNow();
        var instance = new ServiceInstance
        {
            InstanceId = instanceId,
            ServiceId = request.ServiceId,
            PlanId = request.PlanId,
            Parameters = request.Parameters,
            State = ServiceInstanceState.Pending,
            LastOperationDescription = "Provision accepted; waiting for worker.",
            CreatedAt = now,
            UpdatedAt = now
        };
        await _repository.CreateAsync(instance, ct).ConfigureAwait(false);

        var @event = new ProvisioningRequestedEvent
        {
            InstanceId = instanceId,
            ServiceId = request.ServiceId,
            PlanId = request.PlanId,
            UpstreamService = request.Parameters.UpstreamService,
            UpstreamPort = request.Parameters.UpstreamPort,
            Hostname = request.Parameters.Hostname,
            ListenerPort = request.Parameters.ListenerPort,
            PreferredRegions = request.Parameters.Regions
        };
        await _publisher.PublishProvisioningRequestedAsync(@event, ct).ConfigureAwait(false);

        _logger.LogInformation("Provision accepted for {InstanceId} — published {EventId}.", instanceId, @event.EventId);
        return new ProvisionResult(
            ProvisionOutcome.AcceptedAsync,
            Operation: $"provision:{@event.EventId}",
            DashboardUrl: $"/dashboard/{instanceId}");
    }

    public async Task<ProvisionResult> DeprovisionAsync(string instanceId, string serviceId, string planId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var existing = await _repository.GetByIdAsync(instanceId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            _logger.LogInformation("Deprovision for unknown {InstanceId} — returning 410 Gone.", instanceId);
            return new ProvisionResult(ProvisionOutcome.Gone);
        }

        await _repository.UpdateStateAsync(
            instanceId,
            ServiceInstanceState.Deprovisioning,
            "Deprovision accepted; waiting for worker.",
            ct).ConfigureAwait(false);

        var @event = new DeprovisioningRequestedEvent
        {
            InstanceId = instanceId,
            ServiceId = serviceId,
            PlanId = planId
        };
        await _publisher.PublishDeprovisioningRequestedAsync(@event, ct).ConfigureAwait(false);

        _logger.LogInformation("Deprovision accepted for {InstanceId} — published {EventId}.", instanceId, @event.EventId);
        return new ProvisionResult(ProvisionOutcome.AcceptedAsync, Operation: $"deprovision:{@event.EventId}");
    }

    public Task<ServiceInstance?> GetAsync(string instanceId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        return _repository.GetByIdAsync(instanceId, ct);
    }

    public async Task<LastOperationResponse?> GetLastOperationAsync(string instanceId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var instance = await _repository.GetByIdAsync(instanceId, ct).ConfigureAwait(false);
        if (instance is null)
        {
            return null;
        }

        var state = instance.State switch
        {
            ServiceInstanceState.Provisioned => OsbOperationStates.Succeeded,
            ServiceInstanceState.Deprovisioned => OsbOperationStates.Succeeded,
            ServiceInstanceState.Failed => OsbOperationStates.Failed,
            _ => OsbOperationStates.InProgress
        };

        return new LastOperationResponse
        {
            State = state,
            Description = instance.LastOperationDescription
        };
    }

    public async Task ApplyTerminalStateAsync(string instanceId, ServiceInstanceState state, string? description, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var existing = await _repository.GetByIdAsync(instanceId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            _logger.LogWarning("ApplyTerminalState called for unknown {InstanceId}; ignoring.", instanceId);
            return;
        }

        if (state == ServiceInstanceState.Deprovisioned)
        {
            await _repository.DeleteAsync(instanceId, ct).ConfigureAwait(false);
            _logger.LogInformation("Instance {InstanceId} deleted on Deprovisioned state.", instanceId);
            return;
        }

        await _repository.UpdateStateAsync(instanceId, state, description, ct).ConfigureAwait(false);
        _logger.LogInformation("Instance {InstanceId} transitioned to {State}.", instanceId, state);
    }
}
