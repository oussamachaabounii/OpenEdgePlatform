using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenEdgePlatform.ServiceBroker.Api.Middleware;
using OpenEdgePlatform.ServiceBroker.Core.Interfaces;
using OpenEdgePlatform.ServiceBroker.Core.Models;
using OpenEdgePlatform.ServiceBroker.Core.Services;
using OpenEdgePlatform.ServiceBroker.Infrastructure.Messaging;
using OpenEdgePlatform.ServiceBroker.Infrastructure.Persistence;
using OpenEdgePlatform.ServiceBroker.Infrastructure.Persistence.Repositories;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "service-broker")
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(TimeProvider.System);

var connection = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Database=edge_platform;Username=postgres;Password=postgres";
builder.Services.AddDbContext<ServiceBrokerDbContext>(opt => opt.UseNpgsql(connection));

builder.Services.AddScoped<IServiceInstanceRepository, ServiceInstanceRepository>();
builder.Services.AddScoped<IServiceInstanceService, ServiceInstanceService>();
builder.Services.AddScoped<IProvisioningPublisher, RabbitMqProvisioningPublisher>();

builder.Services.AddMassTransit(mt =>
{
    mt.UsingRabbitMq((ctx, cfg) =>
    {
        var rabbit = builder.Configuration.GetConnectionString("RabbitMq") ?? "amqp://guest:guest@localhost:5672";
        cfg.Host(new Uri(rabbit));
        cfg.Message<ProvisioningRequestedEvent>(m => m.SetEntityName(MessagingTopics.ProvisioningRequested));
        cfg.Message<DeprovisioningRequestedEvent>(m => m.SetEntityName(MessagingTopics.DeprovisioningRequested));
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("service-broker"))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter())
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<OsbVersionMiddleware>();
app.MapControllers();
app.MapPrometheusScrapingEndpoint();

if (builder.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ServiceBrokerDbContext>();
    await db.Database.MigrateAsync().ConfigureAwait(false);
}

await app.RunAsync().ConfigureAwait(false);

/// <summary>Exposed so <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/> can find the host.</summary>
public partial class Program;
