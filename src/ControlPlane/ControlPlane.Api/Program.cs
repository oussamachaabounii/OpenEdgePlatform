using MassTransit;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using OpenEdgePlatform.ControlPlane.Api.GrpcServices;
using OpenEdgePlatform.ControlPlane.Core.Interfaces;
using OpenEdgePlatform.ControlPlane.Core.Models;
using OpenEdgePlatform.ControlPlane.Core.Services;
using OpenEdgePlatform.ControlPlane.Infrastructure.Cache;
using OpenEdgePlatform.ControlPlane.Infrastructure.Messaging;
using OpenEdgePlatform.ControlPlane.Infrastructure.Persistence;
using OpenEdgePlatform.ControlPlane.Infrastructure.Persistence.Repositories;
using OpenEdgePlatform.ServiceBroker.Core.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "control-plane")
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

builder.Services.Configure<ControlPlaneOptions>(builder.Configuration.GetSection(ControlPlaneOptions.SectionName));
var options = builder.Configuration.GetSection(ControlPlaneOptions.SectionName).Get<ControlPlaneOptions>() ?? new();

builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenAnyIP(options.RestPort, lo => lo.Protocols = HttpProtocols.Http1AndHttp2);
    k.ListenAnyIP(options.GrpcPort, lo => lo.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connection = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Database=edge_platform;Username=postgres;Password=postgres";
builder.Services.AddDbContext<ControlPlaneDbContext>(opt => opt.UseNpgsql(connection));

if (options.EnableRedisCache)
{
    var redis = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var config = ConfigurationOptions.Parse(redis);
        config.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(config);
    });
}

builder.Services.AddSingleton<RedisSnapshotCache>();
builder.Services.AddScoped<IXdsResourceRepository, XdsResourceRepository>();
builder.Services.AddSingleton<ISnapshotVersioning, SnapshotVersioningService>();
builder.Services.AddSingleton<IXdsConfigGeneratorService, XdsConfigGeneratorService>();
builder.Services.AddSingleton<AdsGrpcService>();
builder.Services.AddSingleton<IXdsSnapshotPublisher>(sp => sp.GetRequiredService<AdsGrpcService>());

builder.Services.AddMassTransit(mt =>
{
    mt.AddConsumer<ProvisioningCompletedConsumer>();
    mt.UsingRabbitMq((ctx, cfg) =>
    {
        var rabbit = builder.Configuration.GetConnectionString("RabbitMq") ?? "amqp://guest:guest@localhost:5672";
        cfg.Host(new Uri(rabbit));

        cfg.Message<ProvisioningCompletedEvent>(m => m.SetEntityName(MessagingTopics.ProvisioningCompleted));

        cfg.ReceiveEndpoint("control-plane.provisioning-completed", e =>
        {
            e.PrefetchCount = 16;
            e.Bind(MessagingTopics.ProvisioningCompleted);
            e.ConfigureConsumer<ProvisioningCompletedConsumer>(ctx);
        });
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("control-plane"))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter())
    .WithTracing(t => t.AddAspNetCoreInstrumentation());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.MapGrpcService<AdsGrpcService>();
app.MapControllers();
app.MapPrometheusScrapingEndpoint();

if (builder.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
    await db.Database.MigrateAsync().ConfigureAwait(false);
}

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
