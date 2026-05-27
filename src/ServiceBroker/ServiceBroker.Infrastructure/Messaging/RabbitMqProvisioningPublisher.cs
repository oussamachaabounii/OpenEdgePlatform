using MassTransit;
using Microsoft.Extensions.Logging;
using OpenEdgePlatform.ServiceBroker.Core.Interfaces;
using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.ServiceBroker.Infrastructure.Messaging;

/// <summary>
/// MassTransit-backed <see cref="IProvisioningPublisher"/>. Uses the bus's <see cref="IPublishEndpoint"/>,
/// so a swap from RabbitMQ to Azure Service Bus is purely a startup-time configuration change.
/// </summary>
public sealed class RabbitMqProvisioningPublisher : IProvisioningPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<RabbitMqProvisioningPublisher> _logger;

    public RabbitMqProvisioningPublisher(IPublishEndpoint publishEndpoint, ILogger<RabbitMqProvisioningPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishProvisioningRequestedAsync(ProvisioningRequestedEvent @event, CancellationToken ct = default)
    {
        await _publishEndpoint.Publish(@event, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Published ProvisioningRequested for {InstanceId} ({EventId}).",
            @event.InstanceId, @event.EventId);
    }

    public async Task PublishDeprovisioningRequestedAsync(DeprovisioningRequestedEvent @event, CancellationToken ct = default)
    {
        await _publishEndpoint.Publish(@event, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Published DeprovisioningRequested for {InstanceId} ({EventId}).",
            @event.InstanceId, @event.EventId);
    }
}
