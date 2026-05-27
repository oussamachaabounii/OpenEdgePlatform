using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenEdgePlatform.Provisioning.Core.Interfaces;
using OpenEdgePlatform.Provisioning.Core.Models;
using OpenEdgePlatform.ProxyConfig.Core.Builders;

namespace OpenEdgePlatform.Provisioning.Core.Services;

/// <summary>
/// Resolves upstream service names via the platform DNS resolver. Each resolved address is fanned out
/// across the requested regions so that the resulting xDS endpoint set covers every (region × address) tuple.
/// </summary>
public sealed class DnsUpstreamResolver : IUpstreamResolver
{
    private readonly ProvisioningOptions _options;
    private readonly ILogger<DnsUpstreamResolver> _logger;

    public DnsUpstreamResolver(IOptions<ProvisioningOptions> options, ILogger<DnsUpstreamResolver> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UpstreamEndpoint>> ResolveAsync(
        string serviceName,
        int port,
        IReadOnlyList<string> regions,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        if (regions.Count == 0)
        {
            throw new ArgumentException("At least one region is required.", nameof(regions));
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(serviceName, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new UpstreamResolutionException($"DNS resolution failed for '{serviceName}'.", ex);
        }

        if (addresses.Length == 0)
        {
            throw new UpstreamResolutionException($"DNS resolution returned no records for '{serviceName}'.");
        }

        var ipv4 = addresses
            .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Take(_options.MaxEndpointsPerService)
            .ToArray();

        if (ipv4.Length == 0)
        {
            ipv4 = addresses.Take(_options.MaxEndpointsPerService).ToArray();
        }

        var weightPerRegion = Math.Max(1, 100 / Math.Max(1, regions.Count));
        var endpoints = new List<UpstreamEndpoint>(ipv4.Length * regions.Count);
        foreach (var address in ipv4)
        {
            foreach (var region in regions)
            {
                endpoints.Add(new UpstreamEndpoint(
                    Host: address.ToString(),
                    Port: port,
                    Region: region,
                    Zone: $"{region}a",
                    Weight: weightPerRegion));
            }
        }

        _logger.LogInformation(
            "Resolved {Service}:{Port} → {Count} endpoint(s) across {Regions} region(s).",
            serviceName, port, endpoints.Count, regions.Count);
        return endpoints;
    }
}

public sealed class UpstreamResolutionException : Exception
{
    public UpstreamResolutionException(string message) : base(message) { }
    public UpstreamResolutionException(string message, Exception inner) : base(message, inner) { }
}
