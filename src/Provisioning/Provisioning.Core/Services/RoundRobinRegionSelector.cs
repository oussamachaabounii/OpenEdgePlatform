using Microsoft.Extensions.Options;
using OpenEdgePlatform.Provisioning.Core.Interfaces;
using OpenEdgePlatform.Provisioning.Core.Models;

namespace OpenEdgePlatform.Provisioning.Core.Services;

/// <summary>
/// Thread-safe round-robin region selector. Maintains a single rotating cursor across calls so concurrent
/// provisions distribute roughly evenly even under contention.
/// </summary>
public sealed class RoundRobinRegionSelector : IProxyRegionSelector
{
    private readonly ProvisioningOptions _options;
    private int _cursor;

    public RoundRobinRegionSelector(IOptions<ProvisioningOptions> options)
    {
        _options = options.Value;
        if (_options.AvailableRegions.Count == 0)
        {
            throw new InvalidOperationException("ProvisioningOptions.AvailableRegions must not be empty.");
        }
        if (_options.RegionsPerInstance <= 0)
        {
            throw new InvalidOperationException("ProvisioningOptions.RegionsPerInstance must be > 0.");
        }
    }

    public IReadOnlyList<string> SelectRegions(IReadOnlyList<string>? preferred = null)
    {
        var pool = preferred is { Count: > 0 }
            ? _options.AvailableRegions.Where(r => preferred.Contains(r, StringComparer.OrdinalIgnoreCase)).ToArray()
            : _options.AvailableRegions.ToArray();

        if (pool.Length == 0)
        {
            pool = _options.AvailableRegions.ToArray();
        }

        var take = Math.Min(_options.RegionsPerInstance, pool.Length);
        var start = (Interlocked.Increment(ref _cursor) - 1) % pool.Length;
        if (start < 0)
        {
            start += pool.Length;
        }

        var selected = new string[take];
        for (var i = 0; i < take; i++)
        {
            selected[i] = pool[(start + i) % pool.Length];
        }
        return selected;
    }
}
