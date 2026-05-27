using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenEdgePlatform.ControlPlane.Core.Services;
using OpenEdgePlatform.ProxyConfig.Core.Builders;
using OpenEdgePlatform.ServiceBroker.Core.Models;
using Xunit;

namespace OpenEdgePlatform.ControlPlane.Tests.Unit;

public sealed class XdsConfigGeneratorServiceTests
{
    [Fact]
    public void Generated_snapshot_has_one_listener_one_cluster_one_route_one_endpoint_group()
    {
        var allocation = new EdgeResourceAllocation
        {
            InstanceId = "inst-1",
            ListenerName = "listener_inst-1",
            ClusterName = "cluster_my-svc_inst-1",
            RouteName = "route_inst-1",
            VirtualHostName = "vh_inst-1",
            Hostname = "api.example.com",
            ListenerPort = 443,
            Regions = new[] { "us-east-1" },
            Endpoints = new[] { new UpstreamEndpoint("10.0.0.1", 8080, "us-east-1") }
        };

        var sut = new XdsConfigGeneratorService(NullLogger<XdsConfigGeneratorService>.Instance);
        var snapshot = sut.GenerateSnapshot(allocation, "v1");

        snapshot.Version.Should().Be("v1");
        snapshot.Listeners.Should().ContainSingle(l => l.Name == "listener_inst-1");
        snapshot.Clusters.Should().ContainSingle(c => c.Name == "cluster_my-svc_inst-1");
        snapshot.Routes.Should().ContainSingle(r => r.Name == "route_inst-1");
        snapshot.Endpoints.Should().ContainSingle(e => e.ClusterName == "cluster_my-svc_inst-1");
    }

    [Fact]
    public void Generator_propagates_listener_port_and_hostname()
    {
        var allocation = new EdgeResourceAllocation
        {
            InstanceId = "i",
            ListenerName = "l",
            ClusterName = "c",
            RouteName = "r",
            VirtualHostName = "vh",
            Hostname = "edge.example",
            ListenerPort = 8443,
            Regions = new[] { "r1" },
            Endpoints = new[] { new UpstreamEndpoint("1.1.1.1", 80, "r1") }
        };

        var sut = new XdsConfigGeneratorService(NullLogger<XdsConfigGeneratorService>.Instance);
        var snapshot = sut.GenerateSnapshot(allocation, "1");

        snapshot.Listeners[0].Address.SocketAddress.PortValue.Should().Be(8443);
        snapshot.Routes[0].VirtualHosts[0].Domains.Should().ContainSingle().Which.Should().Be("edge.example");
    }
}
