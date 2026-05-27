using OpenEdgePlatform.ProxyConfig.Core.Models;

namespace OpenEdgePlatform.ProxyConfig.Core.Builders;

/// <summary>
/// Fluent builder for <see cref="XdsSnapshot"/>. Validates internal consistency at
/// <see cref="Build"/> time and throws <see cref="InvalidXdsSnapshotException"/> on any
/// missing references between resources.
/// </summary>
public sealed class XdsSnapshotBuilder
{
    private readonly List<XdsListener> _listeners = [];
    private readonly List<XdsCluster> _clusters = [];
    private readonly List<XdsRouteConfiguration> _routes = [];
    private readonly List<XdsClusterLoadAssignment> _endpoints = [];
    private string? _instanceId;

    public XdsSnapshotBuilder ForInstance(string instanceId)
    {
        _instanceId = instanceId;
        return this;
    }

    /// <summary>Adds a TCP listener that delegates routing to RDS via a single HCM filter chain.</summary>
    public XdsSnapshotBuilder WithListener(string name, int port, string routeConfigName, string address = "0.0.0.0")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Listener name is required.", nameof(name));
        }
        if (port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Listener port must be 1-65535.");
        }

        var listener = new XdsListener
        {
            Name = name,
            Address = new XdsAddress
            {
                SocketAddress = new SocketAddress { Address = address, PortValue = port }
            },
            FilterChains = new[]
            {
                new FilterChain
                {
                    Filters = new[]
                    {
                        new XdsFilter
                        {
                            Name = "envoy.filters.network.http_connection_manager",
                            TypedConfig = new TypedConfig
                            {
                                TypeUrl = XdsTypeUrls.HttpConnectionManager,
                                StatPrefix = $"ingress_{name}",
                                CodecType = Models.CodecType.AUTO,
                                Rds = new RdsConfig { RouteConfigName = routeConfigName },
                                HttpFilters = new[]
                                {
                                    new HttpFilter
                                    {
                                        Name = "envoy.filters.http.router",
                                        TypedConfig = new { type_url = XdsTypeUrls.Router }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        _listeners.Add(listener);
        return this;
    }

    public XdsSnapshotBuilder WithCluster(string name, LbPolicy policy = LbPolicy.ROUND_ROBIN)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Cluster name is required.", nameof(name));
        }

        _clusters.Add(new XdsCluster
        {
            Name = name,
            Type = ClusterDiscoveryType.EDS,
            LbPolicy = policy,
            EdsClusterConfig = new EdsClusterConfig { ServiceName = name },
            HealthChecks = new[]
            {
                new HealthCheck
                {
                    HttpHealthCheck = new HttpHealthCheck { Path = "/health" }
                }
            }
        });
        return this;
    }

    public XdsSnapshotBuilder WithRoute(string name, string virtualHostName, IEnumerable<string> domains, string cluster, string pathPrefix = "/")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Route name is required.", nameof(name));
        }
        if (string.IsNullOrWhiteSpace(cluster))
        {
            throw new ArgumentException("Route must target a cluster.", nameof(cluster));
        }

        _routes.Add(new XdsRouteConfiguration
        {
            Name = name,
            VirtualHosts = new[]
            {
                new VirtualHost
                {
                    Name = virtualHostName,
                    Domains = domains.ToArray(),
                    Routes = new[]
                    {
                        new Route
                        {
                            Match = new RouteMatch { Prefix = pathPrefix },
                            RouteAction = new RouteAction
                            {
                                Cluster = cluster,
                                RetryPolicy = new RetryPolicy()
                            }
                        }
                    }
                }
            }
        });
        return this;
    }

    public XdsSnapshotBuilder WithEndpoints(string clusterName, IEnumerable<UpstreamEndpoint> endpoints)
    {
        if (string.IsNullOrWhiteSpace(clusterName))
        {
            throw new ArgumentException("Cluster name is required.", nameof(clusterName));
        }

        var grouped = endpoints
            .GroupBy(e => new { e.Region, e.Zone })
            .Select(g => new LocalityLbEndpoints
            {
                Locality = new Locality { Region = g.Key.Region, Zone = g.Key.Zone ?? string.Empty },
                LbEndpoints = g.Select(e => new LbEndpoint
                {
                    Endpoint = new Endpoint
                    {
                        Address = new XdsAddress
                        {
                            SocketAddress = new SocketAddress { Address = e.Host, PortValue = e.Port }
                        }
                    },
                    LoadBalancingWeight = e.Weight,
                    HealthStatus = HealthStatus.HEALTHY
                }).ToArray()
            })
            .ToArray();

        _endpoints.Add(new XdsClusterLoadAssignment
        {
            ClusterName = clusterName,
            Endpoints = grouped
        });
        return this;
    }

    /// <summary>Builds and validates the snapshot. Throws <see cref="InvalidXdsSnapshotException"/> on inconsistency.</summary>
    public XdsSnapshot Build(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Snapshot version is required.", nameof(version));
        }

        var clusterNames = _clusters.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var route in _routes)
        {
            foreach (var vh in route.VirtualHosts)
            {
                foreach (var r in vh.Routes)
                {
                    var target = r.RouteAction?.Cluster;
                    if (!string.IsNullOrEmpty(target) && !clusterNames.Contains(target))
                    {
                        throw new InvalidXdsSnapshotException(
                            $"Route '{route.Name}' references unknown cluster '{target}'.");
                    }
                }
            }
        }

        foreach (var ep in _endpoints)
        {
            if (!clusterNames.Contains(ep.ClusterName))
            {
                throw new InvalidXdsSnapshotException(
                    $"ClusterLoadAssignment references unknown cluster '{ep.ClusterName}'.");
            }
        }

        return new XdsSnapshot
        {
            Version = version,
            Listeners = _listeners.ToArray(),
            Clusters = _clusters.ToArray(),
            Routes = _routes.ToArray(),
            Endpoints = _endpoints.ToArray(),
            InstanceId = _instanceId
        };
    }
}

/// <summary>A single upstream endpoint resolved by the provisioning worker.</summary>
public sealed record UpstreamEndpoint(string Host, int Port, string Region, string? Zone = null, int Weight = 100);
