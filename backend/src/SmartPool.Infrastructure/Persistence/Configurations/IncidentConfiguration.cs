using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> entity)
    {
        entity.HasKey(e => e.Id).HasName("incidents_pkey");
        entity.ToTable("incidents", "smart_pool");
        entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()").HasColumnName("id");
        entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        entity.Property(e => e.Description).HasColumnName("description");
        entity.Property(e => e.IncidentTime).HasDefaultValueSql("now()").HasColumnName("incident_time");
        entity.Property(e => e.ReportedBy).HasColumnName("reported_by");
        entity.Property(e => e.SeverityLevel).HasMaxLength(50).HasColumnName("severity_level");
        entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Pending'::character varying").HasColumnName("status");
        entity.Property(e => e.Title).HasMaxLength(255).HasColumnName("title");
        entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
        entity.HasOne(d => d.ReportedByNavigation).WithMany(p => p.Incidents).HasForeignKey(d => d.ReportedBy).HasConstraintName("incidents_reported_by_fkey");
        entity.Property(e => e.ImageIncidents).HasColumnName("image_incidents");
    }
}
