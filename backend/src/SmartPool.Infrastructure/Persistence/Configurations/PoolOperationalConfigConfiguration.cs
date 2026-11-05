using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public class PoolOperationalConfigConfiguration : IEntityTypeConfiguration<PoolOperationalConfig>
{
    public void Configure(EntityTypeBuilder<PoolOperationalConfig> builder)
    {
        builder.ToTable("PoolOperationalConfigs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.PoolName)
            .IsRequired()
            .HasMaxLength(150)
            .HasDefaultValue("Bể bơi Thành Minh");

        builder.Property(e => e.MaxCapacity)
            .IsRequired()
            .HasDefaultValue(100);

        builder.Property(e => e.WarningThresholdPercent)
            .IsRequired()
            .HasDefaultValue(80);

        builder.Property(e => e.OperationalStatus)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Operating");

        builder.Property(e => e.StatusReason)
            .HasMaxLength(500);

        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(100);

        // Seed 1 bản ghi cấu hình mặc định duy nhất cho bể bơi
        builder.HasData(new PoolOperationalConfig
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            PoolName = "Bể bơi Thành Minh",
            MaxCapacity = 100,
            WarningThresholdPercent = 80,
            MorningStartTime = new TimeOnly(6, 0),
            MorningEndTime = new TimeOnly(11, 30),
            AfternoonStartTime = new TimeOnly(13, 30),
            AfternoonEndTime = new TimeOnly(19, 30),
            OperationalStatus = "Operating",
            StatusReason = null,
            IsAutoSuspended = false,
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedBy = "System"
        });
    }
}
