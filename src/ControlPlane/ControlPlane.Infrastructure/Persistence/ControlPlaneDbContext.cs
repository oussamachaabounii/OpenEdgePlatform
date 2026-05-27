using Microsoft.EntityFrameworkCore;
using OpenEdgePlatform.ControlPlane.Infrastructure.Persistence.Entities;

namespace OpenEdgePlatform.ControlPlane.Infrastructure.Persistence;

public sealed class ControlPlaneDbContext : DbContext
{
    public ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options) : base(options)
    {
    }

    public DbSet<XdsSnapshotEntity> Snapshots => Set<XdsSnapshotEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<XdsSnapshotEntity>(b =>
        {
            b.ToTable("xds_snapshots");
            b.HasKey(e => e.InstanceId);
            b.Property(e => e.InstanceId).HasMaxLength(64).IsRequired();
            b.Property(e => e.Version).HasMaxLength(32).IsRequired();
            b.Property(e => e.SnapshotJson).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.CreatedAt).IsRequired();
            b.Property(e => e.RowVersion).IsConcurrencyToken();
            b.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_xds_snapshots_created_at");
        });
        base.OnModelCreating(modelBuilder);
    }
}
