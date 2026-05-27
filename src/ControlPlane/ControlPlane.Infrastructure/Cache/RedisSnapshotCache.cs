using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenEdgePlatform.ControlPlane.Core.Models;
using OpenEdgePlatform.ProxyConfig.Core.Models;
using StackExchange.Redis;

namespace OpenEdgePlatform.ControlPlane.Infrastructure.Cache;

/// <summary>
/// Thin wrapper around Redis used as a read-through cache for xDS snapshots. Falls back
/// silently when Redis is unavailable so the database remains the source of truth.
/// </summary>
public sealed class RedisSnapshotCache
{
    private const string KeyPrefix = "xds:snapshot:";

    private readonly IConnectionMultiplexer? _redis;
    private readonly ControlPlaneOptions _options;
    private readonly ILogger<RedisSnapshotCache> _logger;

    public RedisSnapshotCache(IOptions<ControlPlaneOptions> options, ILogger<RedisSnapshotCache> logger, IConnectionMultiplexer? redis = null)
    {
        _options = options.Value;
        _logger = logger;
        _redis = _options.EnableRedisCache ? redis : null;
    }

    public bool IsEnabled => _redis is not null;

    public async Task<XdsSnapshot?> TryGetAsync(string instanceId, CancellationToken ct = default)
    {
        if (_redis is null)
        {
            return null;
        }
        try
        {
            var db = _redis.GetDatabase();
            var raw = await db.StringGetAsync(KeyPrefix + instanceId).WaitAsync(ct).ConfigureAwait(false);
            if (raw.IsNullOrEmpty)
            {
                return null;
            }
            return JsonSerializer.Deserialize<XdsSnapshot>(raw!);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis cache get failed for {InstanceId}; falling back to DB.", instanceId);
            return null;
        }
    }

    public async Task SetAsync(string instanceId, XdsSnapshot snapshot, CancellationToken ct = default)
    {
        if (_redis is null)
        {
            return;
        }
        try
        {
            var db = _redis.GetDatabase();
            var payload = JsonSerializer.Serialize(snapshot);
            await db.StringSetAsync(KeyPrefix + instanceId, payload, _options.SnapshotCacheTtl).WaitAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis cache set failed for {InstanceId}; ignoring.", instanceId);
        }
    }

    public async Task InvalidateAsync(string instanceId, CancellationToken ct = default)
    {
        if (_redis is null)
        {
            return;
        }
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(KeyPrefix + instanceId).WaitAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis cache invalidate failed for {InstanceId}; ignoring.", instanceId);
        }
    }
}
