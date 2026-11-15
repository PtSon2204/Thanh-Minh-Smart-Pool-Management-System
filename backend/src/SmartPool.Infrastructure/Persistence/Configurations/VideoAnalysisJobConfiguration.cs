using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class VideoAnalysisJobConfiguration : IEntityTypeConfiguration<VideoAnalysisJob>
{
    public void Configure(EntityTypeBuilder<VideoAnalysisJob> entity)
    {
        entity.ToTable("video_analysis_jobs", "smart_pool");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.UploadedBy).HasColumnName("uploaded_by");
        entity.Property(x => x.OriginalName).HasColumnName("original_name").HasMaxLength(255);
        entity.Property(x => x.InputFile).HasColumnName("input_file").HasMaxLength(200);
        entity.Property(x => x.OutputFile).HasColumnName("output_file").HasMaxLength(200);
        entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
        entity.Property(x => x.Progress).HasColumnName("progress");
        entity.Property(x => x.Error).HasColumnName("error").HasMaxLength(500);
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.HasIndex(x => new { x.Status, x.CreatedAt });
        entity.HasOne(x => x.Uploader).WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
