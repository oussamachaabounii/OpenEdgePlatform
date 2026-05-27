using FluentAssertions;
using OpenEdgePlatform.ProxyConfig.Core.Builders;
using OpenEdgePlatform.ProxyConfig.Core.Models;
using OpenEdgePlatform.ProxyConfig.Infrastructure.Serialization;
using Xunit;

namespace OpenEdgePlatform.ProxyConfig.Tests.Unit;

public sealed class XdsJsonSerializerTests
{
    [Fact]
    public void Serialize_uses_envoy_snake_case_keys()
    {
        var snapshot = new XdsSnapshotBuilder()
            .WithListener("l1", 443, "r1")
            .WithCluster("c1")
            .WithRoute("r1", "vh1", new[] { "example.com" }, "c1")
            .Build("1");

        var json = XdsJsonSerializer.Serialize(snapshot);

        json.Should().Contain("\"filter_chains\"");
        json.Should().Contain("\"socket_address\"");
        json.Should().Contain("\"port_value\"");
        json.Should().Contain("\"virtual_hosts\"");
    }

    [Fact]
    public void Validate_throws_on_missing_version()
    {
        var snapshot = new XdsSnapshot { Version = "" };
        var act = () => XdsJsonSerializer.Validate(snapshot);
        act.Should().Throw<InvalidXdsSnapshotException>().WithMessage("*version*");
    }

    [Fact]
    public void Validate_throws_on_listener_with_zero_port()
    {
        var snapshot = new XdsSnapshot
        {
            Version = "1",
            Listeners = new[]
            {
                new XdsListener
                {
                    Name = "broken",
                    Address = new XdsAddress { SocketAddress = new SocketAddress { Address = "0.0.0.0", PortValue = 0 } }
                }
            }
        };

        var act = () => XdsJsonSerializer.Validate(snapshot);
        act.Should().Throw<InvalidXdsSnapshotException>().WithMessage("*invalid socket address*");
    }

    [Fact]
    public void Serialize_passes_for_well_formed_snapshot()
    {
        var snapshot = new XdsSnapshotBuilder()
            .WithListener("listener_a", 443, "route_a")
            .WithCluster("cluster_a")
            .WithRoute("route_a", "vh_a", new[] { "*" }, "cluster_a")
            .Build("v1");

        var act = () => XdsJsonSerializer.Serialize(snapshot);
        act.Should().NotThrow();
    }
}
