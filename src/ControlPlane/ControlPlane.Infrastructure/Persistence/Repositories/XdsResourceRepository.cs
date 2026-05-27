using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenEdgePlatform.ControlPlane.Core.Interfaces;
using OpenEdgePlatform.ControlPlane.Core.Models;
using OpenEdgePlatform.ControlPlane.Infrastructure.Cache;
using OpenEdgePlatform.ControlPlane.Infrastructure.Persistence.Entities;
using OpenEdgePlatform.ProxyConfig.Core.Models;

namespace OpenEdgePlatform.ControlPlane.Infrastructure.Persistence.Repositories;

public sealed class XdsResourceRepository : IXdsResourceRepository
{
    private readonly ControlPlaneDbContext _db;
    private readonly RedisSnapshotCache _cache;
    private readonly ILogger<XdsResourceRepository> _logger;

    public XdsResourceRepository(ControlPlaneDbContext db, RedisSnapshotCache cache, ILogger<XdsResourceRepository> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task UpsertAsync(string instanceId, XdsSnapshot snapshot, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(snapshot);
        var now = DateTimeOffset.UtcNow;

        var existing = await _db.Snapshots.FirstOrDefaultAsync(s => s.InstanceId == instanceId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            _db.Snapshots.Add(new XdsSnapshotEntity
            {
                InstanceId = instanceId,
                Version = snapshot.Version,
                SnapshotJson = json,
                CreatedAt = now
            });
        }
        else
        {
            existing.Version = snapshot.Version;
            existing.SnapshotJson = json;
            existing.CreatedAt = now;
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await _cache.SetAsync(instanceId, snapshot, ct).ConfigureAwait(false);

        _logger.LogInformation("Upserted snapshot {Version} for {InstanceId}.", snapshot.Version, instanceId);
    }

    public async Task<StoredSnapshot?> GetByInstanceAsync(string instanceId, CancellationToken ct = default)
    {
        var cached = await _cache.TryGetAsync(instanceId, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            return new StoredSnapshot(instanceId, cached.Version, cached, DateTimeOffset.UtcNow);
        }

        var entity = await _db.Snapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.InstanceId == instanceId, ct)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<XdsSnapshot>(entity.SnapshotJson)
            ?? throw new InvalidOperationException($"Stored snapshot for {instanceId} could not be deserialized.");
        await _cache.SetAsync(instanceId, snapshot, ct).ConfigureAwait(false);
        return new StoredSnapshot(entity.InstanceId, entity.Version, snapshot, entity.CreatedAt);
    }

    public async Task<IReadOnlyList<StoredSnapshot>> ListAsync(CancellationToken ct = default)
    {
        var entities = await _db.Snapshots
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(e =>
        {
            var snap = JsonSerializer.Deserialize<XdsSnapshot>(e.SnapshotJson)
                ?? throw new InvalidOperationException($"Stored snapshot for {e.InstanceId} could not be deserialized.");
            return new StoredSnapshot(e.InstanceId, e.Version, snap, e.CreatedAt);
        }).ToArray();
    }

    public async Task DeleteAsync(string instanceId, CancellationToken ct = default)
    {
        var entity = await _db.Snapshots.FirstOrDefaultAsync(s => s.InstanceId == instanceId, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }
        _db.Snapshots.Remove(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await _cache.InvalidateAsync(instanceId, ct).ConfigureAwait(false);
    }
}
