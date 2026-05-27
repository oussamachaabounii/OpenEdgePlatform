using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.Provisioning.Core.Interfaces;

/// <summary>Handles a single inbound <see cref="ProvisioningRequestedEvent"/> end-to-end.</summary>
public interface IProvisioningRequestHandler
{
    Task<ProvisioningCompletedEvent> HandleAsync(ProvisioningRequestedEvent @event, CancellationToken ct = default);
}

public interface IDeprovisioningRequestHandler
{
    Task HandleAsync(DeprovisioningRequestedEvent @event, CancellationToken ct = default);
}
