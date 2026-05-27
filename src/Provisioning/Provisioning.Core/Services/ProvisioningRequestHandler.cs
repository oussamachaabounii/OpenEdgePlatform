using Microsoft.Extensions.Logging;
using OpenEdgePlatform.Provisioning.Core.Interfaces;
using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.Provisioning.Core.Services;

public sealed class ProvisioningRequestHandler : IProvisioningRequestHandler
{
    private readonly IProxyRegionSelector _regionSelector;
    private readonly IUpstreamResolver _upstreamResolver;
    private readonly IInstanceStatusUpdater _statusUpdater;
    private readonly ILogger<ProvisioningRequestHandler> _logger;

    public ProvisioningRequestHandler(
        IProxyRegionSelector regionSelector,
        IUpstreamResolver upstreamResolver,
        IInstanceStatusUpdater statusUpdater,
        ILogger<ProvisioningRequestHandler> logger)
    {
        _regionSelector = regionSelector;
        _upstreamResolver = upstreamResolver;
        _statusUpdater = statusUpdater;
        _logger = logger;
    }

    public async Task<ProvisioningCompletedEvent> HandleAsync(ProvisioningRequestedEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            await _statusUpdater.SetProvisioningAsync(@event.InstanceId, ct).ConfigureAwait(false);

            var regions = _regionSelector.SelectRegions(@event.PreferredRegions);
            _logger.LogInformation(
                "Selected regions {Regions} for instance {InstanceId}.",
                string.Join(",", regions), @event.InstanceId);

            var endpoints = await _upstreamResolver.ResolveAsync(
                @event.UpstreamService,
                @event.UpstreamPort,
                regions,
                ct).ConfigureAwait(false);

            var allocation = new EdgeResourceAllocation
            {
                InstanceId = @event.InstanceId,
                ListenerName = $"listener_{@event.InstanceId}",
                ClusterName = $"cluster_{Sanitize(@event.UpstreamService)}_{@event.InstanceId}",
                RouteName = $"route_{@event.InstanceId}",
                VirtualHostName = $"vh_{@event.InstanceId}",
                Hostname = @event.Hostname,
                ListenerPort = @event.ListenerPort,
                Regions = regions,
                Endpoints = endpoints
            };

            var elapsed = DateTimeOffset.UtcNow - startedAt;
            await _statusUpdater.SetProvisionedAsync(
                @event.InstanceId,
                $"Provisioned across {regions.Count} region(s) in {elapsed.TotalMilliseconds:F0}ms.",
                ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Provisioned {InstanceId} in {Ms}ms (listener={Listener}, cluster={Cluster}).",
                @event.InstanceId, (int)elapsed.TotalMilliseconds, allocation.ListenerName, allocation.ClusterName);

            return new ProvisioningCompletedEvent
            {
                InstanceId = @event.InstanceId,
                Allocation = allocation
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Provisioning failed for {InstanceId}.", @event.InstanceId);
            await _statusUpdater.SetFailedAsync(@event.InstanceId, ex.Message, ct).ConfigureAwait(false);
            throw;
        }
    }

    private static string Sanitize(string name) =>
        new(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? char.ToLowerInvariant(c) : '-').ToArray());
}

public sealed class DeprovisioningRequestHandler : IDeprovisioningRequestHandler
{
    private readonly IInstanceStatusUpdater _statusUpdater;
    private readonly ILogger<DeprovisioningRequestHandler> _logger;

    public DeprovisioningRequestHandler(IInstanceStatusUpdater statusUpdater, ILogger<DeprovisioningRequestHandler> logger)
    {
        _statusUpdater = statusUpdater;
        _logger = logger;
    }

    public async Task HandleAsync(DeprovisioningRequestedEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        _logger.LogInformation("Deprovisioning {InstanceId}.", @event.InstanceId);
        await _statusUpdater.SetDeprovisionedAsync(@event.InstanceId, ct).ConfigureAwait(false);
    }
}
