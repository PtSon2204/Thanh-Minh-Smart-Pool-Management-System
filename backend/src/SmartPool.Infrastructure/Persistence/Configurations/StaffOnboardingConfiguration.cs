using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartPool.Domain.Entities;

namespace SmartPool.Infrastructure.Persistence.Configurations;

public sealed class StaffOnboardingConfiguration : IEntityTypeConfiguration<StaffInvitation>,
    IEntityTypeConfiguration<StaffOnboardingRevision>, IEntityTypeConfiguration<StaffOnboardingDocument>,
    IEntityTypeConfiguration<StaffIdentityClaim>, IEntityTypeConfiguration<StaffCorrectionRequest>
{
    public void Configure(EntityTypeBuilder<StaffInvitation> builder)
    {
        builder.ToTable("staff_invitations", table =>
        {
            table.HasCheckConstraint("ck_staff_invitation_status", "\"Status\" IN ('PENDING','COMPLETED','CANCELLED','EXPIRED')");
            table.HasCheckConstraint("ck_staff_invitation_role", "\"TargetRole\" IN ('SALES','LIFEGUARD','COACH')");
            table.HasCheckConstraint("ck_staff_invitation_delivery", "\"DeliveryStatus\" IN ('PENDING','SENT','FAILED')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Email).HasMaxLength(150);
        builder.Property(item => item.TokenDigest).HasMaxLength(64);
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasIndex(item => item.Email).IsUnique().HasFilter("\"Status\" IN ('PENDING','EXPIRED')");
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.CompletedUserId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<StaffOnboardingRevision> builder)
    {
        builder.ToTable("staff_onboarding_revisions", table =>
        {
            table.HasCheckConstraint("ck_staff_revision_owner", "(\"InvitationId\" IS NOT NULL) <> (\"EmployeeId\" IS NOT NULL)");
            table.HasCheckConstraint("ck_staff_revision_state", "\"State\" IN ('DRAFT','SUBMITTED','APPROVED','CHANGES_REQUESTED','REJECTED')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.Property(item => item.CccdDigest).HasMaxLength(64);
        builder.Property(item => item.CorrectionReason).HasMaxLength(1000);
        builder.HasIndex(item => new { item.InvitationId, item.RevisionNumber }).IsUnique();
        builder.HasIndex(item => new { item.EmployeeId, item.RevisionNumber }).IsUnique();
        builder.HasIndex(item => item.EmployeeId).IsUnique().HasFilter("\"State\" IN ('DRAFT','SUBMITTED','CHANGES_REQUESTED')");
        builder.HasOne<StaffInvitation>().WithMany().HasForeignKey(item => item.InvitationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.ReviewerId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<StaffOnboardingDocument> builder)
    {
        builder.ToTable("staff_onboarding_documents", table =>
        {
            table.HasCheckConstraint("ck_staff_document_purpose", "\"Purpose\" IN ('FRONT','BACK','AVATAR')");
            table.HasCheckConstraint("ck_staff_document_private", "\"Purpose\" = 'AVATAR' OR \"DeliveryType\" = 'authenticated'");
            table.HasCheckConstraint("ck_staff_document_deadline", "\"Purpose\" = 'AVATAR' OR (\"DeleteAfterUtc\" IS NOT NULL AND \"DeleteAfterUtc\" = \"UploadedAtUtc\" + interval '30 days')");
            table.HasCheckConstraint("ck_staff_document_deletion", "\"DeletionState\" IN ('UPLOADING','AVAILABLE','PENDING','DELETED')");
        });
        builder.HasKey(item => item.Id);
        builder.HasOne<StaffOnboardingRevision>().WithMany().HasForeignKey(item => item.RevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.RevisionId, item.Purpose }).IsUnique().HasFilter("\"DeletionState\" = 'AVAILABLE'");
        builder.HasIndex(item => new { item.DeletionState, item.RetryAtUtc });
        builder.HasIndex(item => item.DeleteAfterUtc);
    }

    public void Configure(EntityTypeBuilder<StaffIdentityClaim> builder)
    {
        builder.ToTable("staff_identity_claims");
        builder.HasKey(item => item.Digest);
        builder.Property(item => item.Digest).HasMaxLength(64);
        builder.HasIndex(item => item.OwnerId);
    }

    public void Configure(EntityTypeBuilder<StaffCorrectionRequest> builder)
    {
        builder.ToTable("staff_correction_requests", table => table.HasCheckConstraint("ck_staff_correction_state", "\"State\" IN ('REQUESTED','OPENED','CLOSED')"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StaffOnboardingRevision>().WithMany().HasForeignKey(item => item.RevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.EmployeeId).IsUnique().HasFilter("\"State\" IN ('REQUESTED','OPENED')");
    }
}
