using MassTransit;
using Microsoft.Extensions.Logging;
using OpenEdgePlatform.ControlPlane.Core.Interfaces;
using OpenEdgePlatform.ControlPlane.Core.Services;
using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.ControlPlane.Infrastructure.Messaging;

/// <summary>
/// Consumes <see cref="ProvisioningCompletedEvent"/> from the bus, generates an xDS snapshot,
/// persists it, and triggers a live push to all matching connected proxies.
/// </summary>
public sealed class ProvisioningCompletedConsumer : IConsumer<ProvisioningCompletedEvent>
{
    private readonly IXdsConfigGeneratorService _generator;
    private readonly ISnapshotVersioning _versioning;
    private readonly IXdsResourceRepository _repository;
    private readonly IXdsSnapshotPublisher _publisher;
    private readonly ILogger<ProvisioningCompletedConsumer> _logger;

    public ProvisioningCompletedConsumer(
        IXdsConfigGeneratorService generator,
        ISnapshotVersioning versioning,
        IXdsResourceRepository repository,
        IXdsSnapshotPublisher publisher,
        ILogger<ProvisioningCompletedConsumer> logger)
    {
        _generator = generator;
        _versioning = versioning;
        _repository = repository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProvisioningCompletedEvent> context)
    {
        var msg = context.Message;
        var version = _versioning.Next();
        var snapshot = _generator.GenerateSnapshot(msg.Allocation, version);

        await _repository.UpsertAsync(msg.InstanceId, snapshot, context.CancellationToken).ConfigureAwait(false);
        await _publisher.PushAsync(snapshot, msg.Allocation.Regions, context.CancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Snapshot {Version} for {InstanceId} pushed to regions {Regions}.",
            version, msg.InstanceId, string.Join(",", msg.Allocation.Regions));
    }
}
