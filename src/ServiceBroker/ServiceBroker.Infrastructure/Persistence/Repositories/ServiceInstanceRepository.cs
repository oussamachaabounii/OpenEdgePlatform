using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenEdgePlatform.ServiceBroker.Core.Interfaces;
using OpenEdgePlatform.ServiceBroker.Core.Models;
using OpenEdgePlatform.ServiceBroker.Infrastructure.Persistence.Entities;

namespace OpenEdgePlatform.ServiceBroker.Infrastructure.Persistence.Repositories;

public sealed class ServiceInstanceRepository : IServiceInstanceRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ServiceBrokerDbContext _db;

    public ServiceInstanceRepository(ServiceBrokerDbContext db) => _db = db;

    public async Task<ServiceInstance?> GetByIdAsync(string instanceId, CancellationToken ct = default)
    {
        var entity = await _db.ServiceInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.InstanceId == instanceId, ct)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<ServiceInstance> CreateAsync(ServiceInstance instance, CancellationToken ct = default)
    {
        var entity = new ServiceInstanceEntity
        {
            InstanceId = instance.InstanceId,
            ServiceId = instance.ServiceId,
            PlanId = instance.PlanId,
            State = instance.State.ToString(),
            ParametersJson = JsonSerializer.Serialize(instance.Parameters, JsonOptions),
            LastOperationDescription = instance.LastOperationDescription,
            CreatedAt = instance.CreatedAt,
            UpdatedAt = instance.UpdatedAt
        };
        _db.ServiceInstances.Add(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return MapToDomain(entity);
    }

    public async Task UpdateStateAsync(string instanceId, ServiceInstanceState state, string? description = null, CancellationToken ct = default)
    {
        var entity = await _db.ServiceInstances
            .FirstOrDefaultAsync(e => e.InstanceId == instanceId, ct)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        entity.State = state.ToString();
        entity.LastOperationDescription = description ?? entity.LastOperationDescription;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string instanceId, CancellationToken ct = default)
    {
        var entity = await _db.ServiceInstances
            .FirstOrDefaultAsync(e => e.InstanceId == instanceId, ct)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        _db.ServiceInstances.Remove(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static ServiceInstance MapToDomain(ServiceInstanceEntity e)
    {
        var parameters = JsonSerializer.Deserialize<ProvisionParameters>(e.ParametersJson, JsonOptions)
            ?? throw new InvalidOperationException($"Persisted parameters for {e.InstanceId} are corrupt.");

        return new ServiceInstance
        {
            InstanceId = e.InstanceId,
            ServiceId = e.ServiceId,
            PlanId = e.PlanId,
            State = Enum.Parse<ServiceInstanceState>(e.State),
            Parameters = parameters,
            LastOperationDescription = e.LastOperationDescription,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };
    }
}
