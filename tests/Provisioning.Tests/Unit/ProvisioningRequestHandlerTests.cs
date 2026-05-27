using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenEdgePlatform.Provisioning.Core.Interfaces;
using OpenEdgePlatform.Provisioning.Core.Services;
using OpenEdgePlatform.ProxyConfig.Core.Builders;
using OpenEdgePlatform.ServiceBroker.Core.Models;
using Xunit;

namespace OpenEdgePlatform.Provisioning.Tests.Unit;

public sealed class ProvisioningRequestHandlerTests
{
    private readonly Mock<IProxyRegionSelector> _regions = new();
    private readonly Mock<IUpstreamResolver> _resolver = new();
    private readonly Mock<IInstanceStatusUpdater> _status = new();

    private ProvisioningRequestHandler NewHandler() => new(
        _regions.Object,
        _resolver.Object,
        _status.Object,
        NullLogger<ProvisioningRequestHandler>.Instance);

    private static ProvisioningRequestedEvent MakeEvent() => new()
    {
        InstanceId = "inst-abc",
        ServiceId = "svc",
        PlanId = "plan",
        UpstreamService = "MyService",
        UpstreamPort = 8080,
        Hostname = "api.example.com",
        ListenerPort = 443
    };

    [Fact]
    public async Task Happy_path_publishes_allocation_with_deterministic_names()
    {
        _regions.Setup(r => r.SelectRegions(It.IsAny<IReadOnlyList<string>?>())).Returns(new[] { "us-east-1" });
        _resolver.Setup(r => r.ResolveAsync("MyService", 8080, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UpstreamEndpoint("10.0.0.1", 8080, "us-east-1") });

        var completed = await NewHandler().HandleAsync(MakeEvent());

        completed.InstanceId.Should().Be("inst-abc");
        completed.Allocation.ListenerName.Should().Be("listener_inst-abc");
        completed.Allocation.RouteName.Should().Be("route_inst-abc");
        completed.Allocation.ClusterName.Should().Be("cluster_myservice_inst-abc");
        completed.Allocation.Regions.Should().ContainSingle();
    }

    [Fact]
    public async Task Status_transitions_through_provisioning_to_provisioned_on_success()
    {
        _regions.Setup(r => r.SelectRegions(It.IsAny<IReadOnlyList<string>?>())).Returns(new[] { "r1" });
        _resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UpstreamEndpoint("1.1.1.1", 80, "r1") });

        await NewHandler().HandleAsync(MakeEvent());

        _status.Verify(s => s.SetProvisioningAsync("inst-abc", It.IsAny<CancellationToken>()), Times.Once);
        _status.Verify(s => s.SetProvisionedAsync("inst-abc", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Marks_failed_and_rethrows_when_resolver_throws()
    {
        _regions.Setup(r => r.SelectRegions(It.IsAny<IReadOnlyList<string>?>())).Returns(new[] { "r1" });
        _resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UpstreamResolutionException("dns boom"));

        var act = async () => await NewHandler().HandleAsync(MakeEvent());

        await act.Should().ThrowAsync<UpstreamResolutionException>();
        _status.Verify(s => s.SetFailedAsync("inst-abc", "dns boom", It.IsAny<CancellationToken>()), Times.Once);
        _status.Verify(s => s.SetProvisionedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Region_selector_called_with_preferred_regions()
    {
        _regions.Setup(r => r.SelectRegions(It.IsAny<IReadOnlyList<string>?>())).Returns(new[] { "us-east-1" });
        _resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UpstreamEndpoint("1.1.1.1", 80, "us-east-1") });

        var evt = MakeEvent() with { PreferredRegions = new[] { "us-east-1" } };
        await NewHandler().HandleAsync(evt);

        _regions.Verify(r => r.SelectRegions(It.Is<IReadOnlyList<string>?>(p => p != null && p.SequenceEqual(new[] { "us-east-1" }))), Times.Once);
    }
}
