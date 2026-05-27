using OpenEdgePlatform.ProxyConfig.Core.Builders;

namespace OpenEdgePlatform.Provisioning.Core.Interfaces;

/// <summary>Resolves a logical upstream service name into concrete endpoints, one per region.</summary>
public interface IUpstreamResolver
{
    Task<IReadOnlyList<UpstreamEndpoint>> ResolveAsync(
        string serviceName,
        int port,
        IReadOnlyList<string> regions,
        CancellationToken ct = default);
}
