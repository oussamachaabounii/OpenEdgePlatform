namespace OpenEdgePlatform.Provisioning.Core.Interfaces;

/// <summary>
/// Selects which proxy regions a new service instance should be allocated to. Decoupled
/// from any specific strategy (round-robin, latency-aware, capacity-aware).
/// </summary>
public interface IProxyRegionSelector
{
    /// <summary>
    /// Returns a non-empty, deterministic-for-the-call set of regions. Honours <paramref name="preferred"/>
    /// when non-null and non-empty by intersecting with available regions.
    /// </summary>
    IReadOnlyList<string> SelectRegions(IReadOnlyList<string>? preferred = null);
}
