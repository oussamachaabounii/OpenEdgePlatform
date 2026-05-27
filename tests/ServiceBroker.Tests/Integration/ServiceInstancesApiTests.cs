using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenEdgePlatform.ServiceBroker.Api.Middleware;
using OpenEdgePlatform.ServiceBroker.Core.Models;
using OpenEdgePlatform.ServiceBroker.Infrastructure.Persistence;
using Xunit;

namespace OpenEdgePlatform.ServiceBroker.Tests.Integration;

/// <summary>
/// End-to-end HTTP tests against the broker, backed by an in-memory EF provider and the MassTransit
/// in-memory test harness. Avoids container dependencies so CI runs fast.
/// </summary>
public sealed class ServiceInstancesApiTests : IClassFixture<BrokerWebApplicationFactory>
{
    private readonly BrokerWebApplicationFactory _factory;

    public ServiceInstancesApiTests(BrokerWebApplicationFactory factory) => _factory = factory;

    private HttpClient NewClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(OsbVersionMiddleware.HeaderName, OsbVersionMiddleware.SupportedVersion);
        return client;
    }

    [Fact]
    public async Task Provision_returns_202_and_publishes_event()
    {
        var harness = _factory.Services.GetRequiredService<ITestHarness>();
        await harness.Start();

        var client = NewClient();
        var resp = await client.PutAsJsonAsync(
            "/v2/service_instances/it-1?accepts_incomplete=true",
            NewRequest("api.example.com"));

        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await harness.Published.Any<ProvisioningRequestedEvent>()).Should().BeTrue();

        await harness.Stop();
    }

    [Fact]
    public async Task Provision_without_accepts_incomplete_returns_422()
    {
        var client = NewClient();
        var resp = await client.PutAsJsonAsync("/v2/service_instances/sync-1", NewRequest("a.example.com"));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Missing_osb_header_returns_412()
    {
        var client = _factory.CreateClient();
        var resp = await client.PutAsJsonAsync("/v2/service_instances/no-hdr?accepts_incomplete=true", NewRequest("h.example"));
        resp.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task LastOperation_for_missing_instance_returns_410()
    {
        var client = NewClient();
        var resp = await client.GetAsync("/v2/service_instances/never-existed/last_operation");
        resp.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task Provision_then_get_returns_instance_with_state_metadata()
    {
        var client = NewClient();
        var instanceId = $"flow-{Guid.NewGuid():N}";
        await client.PutAsJsonAsync($"/v2/service_instances/{instanceId}?accepts_incomplete=true", NewRequest("flow.example.com"));

        var resp = await client.GetAsync($"/v2/service_instances/{instanceId}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("\"service_id\"").And.Contain("\"plan_id\"");
    }

    private static ProvisionRequest NewRequest(string hostname) => new()
    {
        ServiceId = "svc-edge-lb",
        PlanId = "standard",
        Parameters = new ProvisionParameters
        {
            UpstreamService = "my-service.default.svc",
            UpstreamPort = 8080,
            Hostname = hostname,
            ListenerPort = 443
        }
    };
}

/// <summary>Test host that swaps Postgres for in-memory EF and RabbitMQ for the MassTransit test harness.</summary>
public sealed class BrokerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"sb-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RunMigrationsOnStartup", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ServiceBrokerDbContext>>();
            services.AddDbContext<ServiceBrokerDbContext>(opt => opt.UseInMemoryDatabase(_dbName));

            services.RemoveMassTransitHostedService();
            foreach (var sd in services.Where(s => s.ServiceType.FullName?.StartsWith("MassTransit", StringComparison.Ordinal) == true).ToArray())
            {
                services.Remove(sd);
            }
            services.AddMassTransitTestHarness();
        });
    }
}

internal static class ServiceCollectionExtensions
{
    public static void RemoveAll<TService>(this IServiceCollection services)
    {
        foreach (var sd in services.Where(s => s.ServiceType == typeof(TService)).ToArray())
        {
            services.Remove(sd);
        }
    }

    public static void RemoveMassTransitHostedService(this IServiceCollection services)
    {
        foreach (var sd in services.Where(s => s.ImplementationType?.FullName?.Contains("MassTransit", StringComparison.Ordinal) == true).ToArray())
        {
            services.Remove(sd);
        }
    }
}
