using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenEdgePlatform.ControlPlane.Api.Grpc;
using OpenEdgePlatform.ControlPlane.Core.Interfaces;
using OpenEdgePlatform.ControlPlane.Core.Models;
using OpenEdgePlatform.ProxyConfig.Core.Models;

namespace OpenEdgePlatform.ControlPlane.Api.GrpcServices;

/// <summary>
/// ADS gRPC service. Implements bidirectional streaming over the envoy.service.discovery.v3
/// contract and tracks every connected node's stream so the control plane can push new
/// configuration without restart.
/// </summary>
public sealed class AdsGrpcService :
    AggregatedDiscoveryService.AggregatedDiscoveryServiceBase,
    IXdsSnapshotPublisher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdsGrpcService> _logger;
    private readonly ConcurrentDictionary<string, ConnectedProxy> _connected = new(StringComparer.Ordinal);

    public AdsGrpcService(IServiceScopeFactory scopeFactory, ILogger<AdsGrpcService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async Task StreamAggregatedResources(
        IAsyncStreamReader<DiscoveryRequest> requestStream,
        IServerStreamWriter<DiscoveryResponse> responseStream,
        ServerCallContext context)
    {
        ConnectedProxy? proxy = null;
        try
        {
            await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken).ConfigureAwait(false))
            {
                if (proxy is null && request.Node is { Id.Length: > 0 })
                {
                    proxy = new ConnectedProxy
                    {
                        Node = new ProxyNode
                        {
                            NodeId = request.Node.Id,
                            Cluster = request.Node.Cluster ?? string.Empty,
                            Region = request.Node.Locality?.Region ?? "unknown",
                            Zone = request.Node.Locality?.Zone ?? "unknown",
                            ConnectedAt = DateTimeOffset.UtcNow
                        },
                        ResponseStream = responseStream,
                        WriteLock = new SemaphoreSlim(1, 1)
                    };
                    _connected[proxy.Node.NodeId] = proxy;
                    _logger.LogInformation(
                        "ADS stream opened for node {NodeId} (region={Region}, zone={Zone}).",
                        proxy.Node.NodeId, proxy.Node.Region, proxy.Node.Zone);
                }

                if (proxy is null)
                {
                    _logger.LogWarning("Received DiscoveryRequest without a Node identity; ignoring.");
                    continue;
                }

                if (!string.IsNullOrEmpty(request.ErrorDetail?.Message))
                {
                    _logger.LogWarning(
                        "Proxy {NodeId} reported NACK for {TypeUrl}: code={Code} message={Message}",
                        proxy.Node.NodeId, request.TypeUrl, request.ErrorDetail.Code, request.ErrorDetail.Message);
                    continue;
                }

                await SendForTypeAsync(proxy, request.TypeUrl, context.CancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal disconnect.
        }
        finally
        {
            if (proxy is not null)
            {
                _connected.TryRemove(proxy.Node.NodeId, out _);
                _logger.LogInformation("ADS stream closed for node {NodeId}.", proxy.Node.NodeId);
            }
        }
    }

    public async Task PushAsync(XdsSnapshot snapshot, IReadOnlyList<string> regions, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var targets = _connected.Values
            .Where(p => regions.Count == 0 || regions.Contains(p.Node.Region, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (targets.Length == 0)
        {
            _logger.LogInformation("No proxies connected for regions {Regions}; snapshot {Version} stored only.",
                string.Join(",", regions), snapshot.Version);
            return;
        }

        foreach (var proxy in targets)
        {
            await SendAllTypesAsync(proxy, snapshot, ct).ConfigureAwait(false);
            proxy.Node.CurrentVersion = snapshot.Version;
        }

        _logger.LogInformation(
            "Pushed snapshot {Version} to {Count} proxy(ies).", snapshot.Version, targets.Length);
    }

    public IReadOnlyList<ProxyNodeSummary> ListProxies() =>
        _connected.Values
            .Select(p => new ProxyNodeSummary(p.Node.NodeId, p.Node.Region, p.Node.Zone, p.Node.CurrentVersion, p.Node.ConnectedAt))
            .ToArray();

    private async Task SendForTypeAsync(ConnectedProxy proxy, string typeUrl, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IXdsResourceRepository>();
        var snapshots = await repository.ListAsync(ct).ConfigureAwait(false);
        if (snapshots.Count == 0)
        {
            return;
        }

        // Merge all current snapshots so the proxy sees the full fleet picture.
        var resources = new List<Any>();
        var version = snapshots.Max(s => s.Version) ?? string.Empty;

        foreach (var stored in snapshots)
        {
            foreach (var resource in SelectResources(stored.Snapshot, typeUrl))
            {
                resources.Add(resource);
            }
        }

        await WriteAsync(proxy, new DiscoveryResponse
        {
            VersionInfo = version,
            TypeUrl = typeUrl,
            Nonce = Guid.NewGuid().ToString("N"),
            Resources = { resources }
        }, ct).ConfigureAwait(false);
    }

    private async Task SendAllTypesAsync(ConnectedProxy proxy, XdsSnapshot snapshot, CancellationToken ct)
    {
        foreach (var typeUrl in new[]
                 {
                     XdsTypeUrls.Cluster,
                     XdsTypeUrls.ClusterLoadAssignment,
                     XdsTypeUrls.RouteConfiguration,
                     XdsTypeUrls.Listener,
                 })
        {
            var resources = SelectResources(snapshot, typeUrl).ToArray();
            if (resources.Length == 0)
            {
                continue;
            }
            await WriteAsync(proxy, new DiscoveryResponse
            {
                VersionInfo = snapshot.Version,
                TypeUrl = typeUrl,
                Nonce = Guid.NewGuid().ToString("N"),
                Resources = { resources }
            }, ct).ConfigureAwait(false);
        }
    }

    private static IEnumerable<Any> SelectResources(XdsSnapshot snapshot, string typeUrl) => typeUrl switch
    {
        XdsTypeUrls.Listener => snapshot.Listeners.Select(l => Wrap(typeUrl, l)),
        XdsTypeUrls.Cluster => snapshot.Clusters.Select(c => Wrap(typeUrl, c)),
        XdsTypeUrls.RouteConfiguration => snapshot.Routes.Select(r => Wrap(typeUrl, r)),
        XdsTypeUrls.ClusterLoadAssignment => snapshot.Endpoints.Select(e => Wrap(typeUrl, e)),
        _ => Enumerable.Empty<Any>()
    };

    private static Any Wrap<T>(string typeUrl, T resource)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resource));
        return new Any
        {
            TypeUrl = typeUrl,
            Value = ByteString.CopyFrom(bytes)
        };
    }

    private static async Task WriteAsync(ConnectedProxy proxy, DiscoveryResponse response, CancellationToken ct)
    {
        await proxy.WriteLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await proxy.ResponseStream.WriteAsync(response, ct).ConfigureAwait(false);
        }
        finally
        {
            proxy.WriteLock.Release();
        }
    }

    private sealed class ConnectedProxy
    {
        public required ProxyNode Node { get; init; }
        public required IServerStreamWriter<DiscoveryResponse> ResponseStream { get; init; }
        public required SemaphoreSlim WriteLock { get; init; }
    }
}
