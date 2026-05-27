using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenEdgePlatform.Provisioning.Core.Interfaces;
using OpenEdgePlatform.Provisioning.Core.Models;
using OpenEdgePlatform.Provisioning.Core.Services;
using OpenEdgePlatform.Provisioning.Worker;
using OpenEdgePlatform.ServiceBroker.Core.Interfaces;
using OpenEdgePlatform.ServiceBroker.Core.Models;
using OpenEdgePlatform.ServiceBroker.Infrastructure.Persistence;
using OpenEdgePlatform.ServiceBroker.Infrastructure.Persistence.Repositories;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(lc => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("MassTransit", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "provisioning-worker")
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

builder.Services.Configure<ProvisioningOptions>(builder.Configuration.GetSection(ProvisioningOptions.SectionName));

var connection = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Database=edge_platform;Username=postgres;Password=postgres";
builder.Services.AddDbContext<ServiceBrokerDbContext>(opt => opt.UseNpgsql(connection));

builder.Services.AddScoped<IServiceInstanceRepository, ServiceInstanceRepository>();
builder.Services.AddSingleton<IProxyRegionSelector, RoundRobinRegionSelector>();
builder.Services.AddSingleton<IUpstreamResolver, DnsUpstreamResolver>();
builder.Services.AddScoped<IInstanceStatusUpdater, InstanceStatusUpdater>();
builder.Services.AddScoped<IProvisioningRequestHandler, ProvisioningRequestHandler>();
builder.Services.AddScoped<IDeprovisioningRequestHandler, DeprovisioningRequestHandler>();

builder.Services.AddMassTransit(mt =>
{
    mt.AddConsumer<ProvisioningWorker>(c =>
    {
        c.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
    });
    mt.AddConsumer<DeprovisioningConsumer>();

    mt.UsingRabbitMq((ctx, cfg) =>
    {
        var rabbit = builder.Configuration.GetConnectionString("RabbitMq") ?? "amqp://guest:guest@localhost:5672";
        cfg.Host(new Uri(rabbit));

        cfg.Message<ProvisioningRequestedEvent>(m => m.SetEntityName(MessagingTopics.ProvisioningRequested));
        cfg.Message<ProvisioningCompletedEvent>(m => m.SetEntityName(MessagingTopics.ProvisioningCompleted));
        cfg.Message<ProvisioningFailedEvent>(m => m.SetEntityName(MessagingTopics.ProvisioningFailed));
        cfg.Message<DeprovisioningRequestedEvent>(m => m.SetEntityName(MessagingTopics.DeprovisioningRequested));

        cfg.ReceiveEndpoint("provisioning-worker", e =>
        {
            e.PrefetchCount = 16;
            e.Bind(MessagingTopics.ProvisioningRequested);
            e.ConfigureConsumer<ProvisioningWorker>(ctx);
        });

        cfg.ReceiveEndpoint("deprovisioning-worker", e =>
        {
            e.Bind(MessagingTopics.DeprovisioningRequested);
            e.ConfigureConsumer<DeprovisioningConsumer>(ctx);
        });
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("provisioning-worker"))
    .WithTracing(t => { });

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
