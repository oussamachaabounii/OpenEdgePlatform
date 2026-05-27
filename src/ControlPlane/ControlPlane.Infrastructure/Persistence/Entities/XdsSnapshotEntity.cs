namespace OpenEdgePlatform.ControlPlane.Infrastructure.Persistence.Entities;

public sealed class XdsSnapshotEntity
{
    public string InstanceId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public uint RowVersion { get; set; }
}
