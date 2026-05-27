using OpenEdgePlatform.ControlPlane.Core.Models;
using OpenEdgePlatform.ProxyConfig.Core.Models;

namespace OpenEdgePlatform.ControlPlane.Core.Interfaces;

/// <summary>
/// Persistence + cache for xDS snapshots. Implementations are responsible for write-through to PostgreSQL
/// and a Redis read-through cache with TTL.
/// </summary>
public interface IXdsResourceRepository
{
    Task UpsertAsync(string instanceId, XdsSnapshot snapshot, CancellationToken ct = default);
    Task<StoredSnapshot?> GetByInstanceAsync(string instanceId, CancellationToken ct = default);
    Task<IReadOnlyList<StoredSnapshot>> ListAsync(CancellationToken ct = default);
    Task DeleteAsync(string instanceId, CancellationToken ct = default);
}
