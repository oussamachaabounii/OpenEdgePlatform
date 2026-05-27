using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenEdgePlatform.ServiceBroker.Core.Interfaces;
using OpenEdgePlatform.ServiceBroker.Core.Models;
using OpenEdgePlatform.ServiceBroker.Core.Services;
using Xunit;

namespace OpenEdgePlatform.ServiceBroker.Tests.Unit;

public sealed class ServiceInstanceServiceTests
{
    private readonly Mock<IServiceInstanceRepository> _repo = new();
    private readonly Mock<IProvisioningPublisher> _publisher = new();

    private ServiceInstanceService NewService() => new(
        _repo.Object,
        _publisher.Object,
        NullLogger<ServiceInstanceService>.Instance);

    private static ProvisionRequest MakeRequest(string hostname = "api.example.com") => new()
    {
        ServiceId = "svc-edge-lb",
        PlanId = "standard",
        Parameters = new ProvisionParameters
        {
            UpstreamService = "my-service.default.svc.cluster.local",
            UpstreamPort = 8080,
            Hostname = hostname,
            ListenerPort = 443
        }
    };

    [Fact]
    public async Task Provision_on_new_instance_publishes_event_and_returns_accepted_async()
    {
        _repo.Setup(r => r.GetByIdAsync("i-1", It.IsAny<CancellationToken>())).ReturnsAsync((ServiceInstance?)null);

        var sut = NewService();
        var result = await sut.ProvisionAsync("i-1", MakeRequest());

        result.Outcome.Should().Be(ProvisionOutcome.AcceptedAsync);
        _repo.Verify(r => r.CreateAsync(It.Is<ServiceInstance>(s =>
            s.InstanceId == "i-1" && s.State == ServiceInstanceState.Pending), It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.PublishProvisioningRequestedAsync(It.IsAny<ProvisioningRequestedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Provision_on_existing_identical_returns_already_exists_and_does_not_publish()
    {
        var existing = new ServiceInstance
        {
            InstanceId = "i-1",
            ServiceId = "svc-edge-lb",
            PlanId = "standard",
            State = ServiceInstanceState.Provisioned,
            Parameters = MakeRequest().Parameters!,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _repo.Setup(r => r.GetByIdAsync("i-1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await NewService().ProvisionAsync("i-1", MakeRequest());

        result.Outcome.Should().Be(ProvisionOutcome.AlreadyExistsIdentical);
        _publisher.Verify(p => p.PublishProvisioningRequestedAsync(It.IsAny<ProvisioningRequestedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Provision_on_existing_with_different_params_returns_conflict()
    {
        var existing = new ServiceInstance
        {
            InstanceId = "i-1",
            ServiceId = "svc-edge-lb",
            PlanId = "standard",
            State = ServiceInstanceState.Provisioned,
            Parameters = MakeRequest("api.different.com").Parameters!,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _repo.Setup(r => r.GetByIdAsync("i-1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await NewService().ProvisionAsync("i-1", MakeRequest("api.example.com"));

        result.Outcome.Should().Be(ProvisionOutcome.Conflict);
        _publisher.Verify(p => p.PublishProvisioningRequestedAsync(It.IsAny<ProvisioningRequestedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deprovision_for_missing_instance_returns_gone()
    {
        _repo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((ServiceInstance?)null);

        var result = await NewService().DeprovisionAsync("missing", "svc", "plan");

        result.Outcome.Should().Be(ProvisionOutcome.Gone);
    }

    [Fact]
    public async Task Deprovision_for_existing_publishes_event_and_updates_state()
    {
        var existing = new ServiceInstance
        {
            InstanceId = "i-1",
            ServiceId = "svc",
            PlanId = "plan",
            State = ServiceInstanceState.Provisioned,
            Parameters = MakeRequest().Parameters!,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _repo.Setup(r => r.GetByIdAsync("i-1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await NewService().DeprovisionAsync("i-1", "svc", "plan");

        result.Outcome.Should().Be(ProvisionOutcome.AcceptedAsync);
        _repo.Verify(r => r.UpdateStateAsync("i-1", ServiceInstanceState.Deprovisioning, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.PublishDeprovisioningRequestedAsync(It.IsAny<DeprovisioningRequestedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LastOperation_maps_states_to_osb_strings()
    {
        var states = new Dictionary<ServiceInstanceState, string>
        {
            [ServiceInstanceState.Pending] = OsbOperationStates.InProgress,
            [ServiceInstanceState.Provisioning] = OsbOperationStates.InProgress,
            [ServiceInstanceState.Provisioned] = OsbOperationStates.Succeeded,
            [ServiceInstanceState.Deprovisioning] = OsbOperationStates.InProgress,
            [ServiceInstanceState.Failed] = OsbOperationStates.Failed
        };

        foreach (var (state, expected) in states)
        {
            _repo.Setup(r => r.GetByIdAsync("i", It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceInstance
            {
                InstanceId = "i",
                ServiceId = "s",
                PlanId = "p",
                State = state,
                Parameters = MakeRequest().Parameters!,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });

            var result = await NewService().GetLastOperationAsync("i");
            result!.State.Should().Be(expected);
        }
    }
}
