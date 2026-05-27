using MassTransit;
using Microsoft.Extensions.Logging;
using OpenEdgePlatform.Provisioning.Core.Interfaces;
using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.Provisioning.Worker;

/// <summary>
/// MassTransit consumer that turns a <see cref="ProvisioningRequestedEvent"/> into a
/// <see cref="ProvisioningCompletedEvent"/>. Retries are configured on the receive endpoint;
/// uncaught exceptions are dead-lettered after the final attempt.
/// </summary>
public sealed class ProvisioningWorker : IConsumer<ProvisioningRequestedEvent>
{
    private readonly IProvisioningRequestHandler _handler;
    private readonly ILogger<ProvisioningWorker> _logger;

    public ProvisioningWorker(IProvisioningRequestHandler handler, ILogger<ProvisioningWorker> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProvisioningRequestedEvent> context)
    {
        var request = context.Message;
        _logger.LogInformation(
            "Received ProvisioningRequested for {InstanceId} (event {EventId}, attempt {Redelivery}).",
            request.InstanceId, request.EventId, context.GetRedeliveryCount());

        try
        {
            var completed = await _handler.HandleAsync(request, context.CancellationToken).ConfigureAwait(false);
            await context.Publish(completed).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await context.Publish(new ProvisioningFailedEvent
            {
                InstanceId = request.InstanceId,
                Reason = ex.Message
            }).ConfigureAwait(false);
            throw;
        }
    }
}

public sealed class DeprovisioningConsumer : IConsumer<DeprovisioningRequestedEvent>
{
    private readonly IDeprovisioningRequestHandler _handler;
    private readonly ILogger<DeprovisioningConsumer> _logger;

    public DeprovisioningConsumer(IDeprovisioningRequestHandler handler, ILogger<DeprovisioningConsumer> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeprovisioningRequestedEvent> context)
    {
        _logger.LogInformation("Received DeprovisioningRequested for {InstanceId}.", context.Message.InstanceId);
        await _handler.HandleAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
    }
}
