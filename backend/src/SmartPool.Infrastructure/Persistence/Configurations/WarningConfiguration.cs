using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;
public sealed class WarningConfiguration : IEntityTypeConfiguration<Warning>
{
    public void Configure(EntityTypeBuilder<Warning> entity)
    {
        entity.ToTable("warnings");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("uuid_generate_v4()");
        entity.Property(e => e.CamId).HasColumnName("cam_id");
        entity.Property(e => e.WarningTime).HasColumnName("warning_time").HasDefaultValueSql("NOW()");
        // Không bao giờ null: rỗng cho tới khi ảnh snapshot upload xong.
        entity.Property(e => e.ImageUrl).HasColumnName("image_url").IsRequired().HasDefaultValue("");
        entity.Property(e => e.SeverityLevel).HasColumnName("severity_level").HasMaxLength(50);
        entity.Property(e => e.Confidence).HasColumnName("confidence").HasColumnType("numeric(5, 2)");
        entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).HasDefaultValue("Pending");
        entity.Property(e => e.IncidentId).HasColumnName("incident_id");
        entity.Property(e => e.VerifiedBy).HasColumnName("verified_by");
        entity.Property(e => e.VerifiedAt).HasColumnName("verified_at");
        entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

        // Giám sát an toàn
        entity.Property(e => e.SourceType).HasColumnName("source_type").HasMaxLength(10).HasDefaultValue("LIVE");
        entity.Property(e => e.RiskEpisode).HasColumnName("risk_episode").HasMaxLength(80);
        entity.Property(e => e.TrackId).HasColumnName("track_id").HasMaxLength(80);
        entity.Property(e => e.VideoJobId).HasColumnName("video_job_id");
        entity.Property(e => e.VideoTimestamp).HasColumnName("video_timestamp");
        entity.Property(e => e.SnapshotStatus).HasColumnName("snapshot_status").HasMaxLength(20).HasDefaultValue("NONE");
        entity.Property(e => e.SnapshotAttempts).HasColumnName("snapshot_attempts");
        // Indexes
        entity.HasIndex(e => e.CamId).HasDatabaseName("idx_warnings_cam_id");
        entity.HasIndex(e => e.Status).HasDatabaseName("idx_warnings_status");
        entity.HasIndex(e => e.WarningTime).IsDescending().HasDatabaseName("idx_warnings_time");
        entity.HasIndex(e => e.VideoJobId);
        entity.HasIndex(e => new { e.SourceType, e.RiskEpisode }).IsUnique().HasFilter("risk_episode IS NOT NULL").HasDatabaseName("IX_warnings_source_type_risk_episode");
        entity.HasOne(d => d.Cam).WithMany().HasForeignKey(d => d.CamId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(d => d.Incident).WithMany().HasForeignKey(d => d.IncidentId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(d => d.Verifier).WithMany().HasForeignKey(d => d.VerifiedBy).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(d => d.VideoJob).WithMany().HasForeignKey(d => d.VideoJobId).OnDelete(DeleteBehavior.Restrict);
    }
}
