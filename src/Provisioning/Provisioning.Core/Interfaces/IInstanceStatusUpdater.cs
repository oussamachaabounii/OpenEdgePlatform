using OpenEdgePlatform.ServiceBroker.Core.Models;

namespace OpenEdgePlatform.Provisioning.Core.Interfaces;

/// <summary>
/// Allows the provisioning worker to drive the broker's state machine without depending on the broker's
/// HTTP surface. Backed by the shared persistence layer.
/// </summary>
public interface IInstanceStatusUpdater
{
    Task SetProvisioningAsync(string instanceId, CancellationToken ct = default);
    Task SetProvisionedAsync(string instanceId, string description, CancellationToken ct = default);
    Task SetFailedAsync(string instanceId, string reason, CancellationToken ct = default);
    Task SetDeprovisionedAsync(string instanceId, CancellationToken ct = default);
}
