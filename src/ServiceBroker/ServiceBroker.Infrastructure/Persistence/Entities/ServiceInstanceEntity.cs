namespace OpenEdgePlatform.ServiceBroker.Infrastructure.Persistence.Entities;

/// <summary>EF Core persistence entity for an OSB service instance.</summary>
public sealed class ServiceInstanceEntity
{
    public string InstanceId { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = string.Empty;
    public string? LastOperationDescription { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public uint Version { get; set; }
}
