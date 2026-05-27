using FluentAssertions;
using OpenEdgePlatform.ProxyConfig.Core.Builders;
using OpenEdgePlatform.ProxyConfig.Core.Models;
using Xunit;

namespace OpenEdgePlatform.ProxyConfig.Tests.Unit;

public sealed class XdsSnapshotBuilderTests
{
    [Fact]
    public void Build_with_all_resources_produces_valid_snapshot()
    {
        var endpoints = new[]
        {
            new UpstreamEndpoint("10.0.0.1", 8080, "us-east-1", "us-east-1a"),
            new UpstreamEndpoint("10.0.0.2", 8080, "us-east-1", "us-east-1b"),
            new UpstreamEndpoint("10.0.1.1", 8080, "us-west-2", "us-west-2a")
        };

        var snapshot = new XdsSnapshotBuilder()
            .ForInstance("inst-1")
            .WithListener("listener_inst-1", 443, "route_inst-1")
            .WithCluster("cluster_my-service_inst-1")
            .WithRoute("route_inst-1", "vh_inst-1", new[] { "example.com" }, "cluster_my-service_inst-1")
            .WithEndpoints("cluster_my-service_inst-1", endpoints)
            .Build("12345");

        snapshot.Version.Should().Be("12345");
        snapshot.InstanceId.Should().Be("inst-1");
        snapshot.Listeners.Should().HaveCount(1);
        snapshot.Clusters.Should().HaveCount(1);
        snapshot.Routes.Should().HaveCount(1);
        snapshot.Endpoints.Should().HaveCount(1);

        var locality = snapshot.Endpoints[0].Endpoints;
        locality.Should().HaveCount(3);
        locality.Select(l => l.Locality.Region).Distinct().Should().BeEquivalentTo("us-east-1", "us-west-2");
    }

    [Fact]
    public void Build_with_route_referencing_unknown_cluster_throws()
    {
        var act = () => new XdsSnapshotBuilder()
            .WithRoute("route_x", "vh_x", new[] { "example.com" }, "cluster_missing")
            .Build("1");

        act.Should().Throw<InvalidXdsSnapshotException>()
            .WithMessage("*unknown cluster*");
    }

    [Fact]
    public void Build_with_endpoints_referencing_unknown_cluster_throws()
    {
        var act = () => new XdsSnapshotBuilder()
            .WithEndpoints("cluster_missing", new[] { new UpstreamEndpoint("10.0.0.1", 80, "us-east-1") })
            .Build("1");

        act.Should().Throw<InvalidXdsSnapshotException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void Listener_rejects_invalid_port(int port)
    {
        var act = () => new XdsSnapshotBuilder().WithListener("l", port, "r");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Build_groups_endpoints_by_region()
    {
        var endpoints = new[]
        {
            new UpstreamEndpoint("10.0.0.1", 80, "us-east-1", "us-east-1a"),
            new UpstreamEndpoint("10.0.0.2", 80, "us-east-1", "us-east-1a"),
            new UpstreamEndpoint("10.0.1.1", 80, "us-east-1", "us-east-1b"),
            new UpstreamEndpoint("10.0.2.1", 80, "us-west-2", "us-west-2a")
        };

        var snapshot = new XdsSnapshotBuilder()
            .WithCluster("c1")
            .WithEndpoints("c1", endpoints)
            .Build("1");

        var localities = snapshot.Endpoints[0].Endpoints;
        localities.Should().HaveCount(3);
        localities.Single(l => l.Locality.Zone == "us-east-1a").LbEndpoints.Should().HaveCount(2);
        localities.Single(l => l.Locality.Zone == "us-east-1b").LbEndpoints.Should().HaveCount(1);
    }

    [Fact]
    public void Build_rejects_empty_version()
    {
        var act = () => new XdsSnapshotBuilder().Build("");
        act.Should().Throw<ArgumentException>();
    }
}
