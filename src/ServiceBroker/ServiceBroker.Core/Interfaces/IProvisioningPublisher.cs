using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.ServiceBroker.Core.Interfaces;

/// <summary>
/// Abstraction over the messaging bus for outgoing provisioning lifecycle events.
/// Allows the core service layer to remain RabbitMQ-agnostic.
/// </summary>
public interface IProvisioningPublisher
{
    Task PublishProvisioningRequestedAsync(ProvisioningRequestedEvent @event, CancellationToken ct = default);
    Task PublishDeprovisioningRequestedAsync(DeprovisioningRequestedEvent @event, CancellationToken ct = default);
}
