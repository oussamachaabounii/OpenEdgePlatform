using Microsoft.EntityFrameworkCore;
using OpenEdgePlatform.ServiceBroker.Infrastructure.Persistence.Entities;

namespace OpenEdgePlatform.ServiceBroker.Infrastructure.Persistence;

public sealed class ServiceBrokerDbContext : DbContext
{
    public ServiceBrokerDbContext(DbContextOptions<ServiceBrokerDbContext> options) : base(options)
    {
    }

    public DbSet<ServiceInstanceEntity> ServiceInstances => Set<ServiceInstanceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceInstanceEntity>(b =>
        {
            b.ToTable("service_instances");
            b.HasKey(e => e.InstanceId);

            b.Property(e => e.InstanceId).HasMaxLength(64).IsRequired();
            b.Property(e => e.ServiceId).HasMaxLength(64).IsRequired();
            b.Property(e => e.PlanId).HasMaxLength(64).IsRequired();
            b.Property(e => e.State).HasMaxLength(32).IsRequired();
            b.Property(e => e.ParametersJson).HasColumnType("jsonb").IsRequired();
            b.Property(e => e.LastOperationDescription).HasMaxLength(512);
            b.Property(e => e.CreatedAt).IsRequired();
            b.Property(e => e.UpdatedAt).IsRequired();
            b.Property(e => e.Version).IsConcurrencyToken();

            b.HasIndex(e => e.State).HasDatabaseName("ix_service_instances_state");
            b.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_service_instances_created_at");
        });

        base.OnModelCreating(modelBuilder);
    }
}
