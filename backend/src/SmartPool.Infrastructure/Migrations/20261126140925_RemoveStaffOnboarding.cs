using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStaffOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_employees_staff_onboarding_revisions_correction_revision_id",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.DropTable(
                name: "staff_onboarding_documents",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "staff_onboarding_revisions",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "staff_invitations",
                schema: "smart_pool");

            migrationBuilder.DropIndex(
                name: "IX_employees_correction_revision_id",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.DropCheckConstraint(
                name: "ck_employees_correction_active",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "correction_reason",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "correction_request_id",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "correction_requested_at_utc",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "correction_revision_id",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "correction_version",
                schema: "smart_pool",
                table: "employees");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "correction_reason",
                schema: "smart_pool",
                table: "employees",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "correction_request_id",
                schema: "smart_pool",
                table: "employees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "correction_requested_at_utc",
                schema: "smart_pool",
                table: "employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "correction_revision_id",
                schema: "smart_pool",
                table: "employees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "correction_version",
                schema: "smart_pool",
                table: "employees",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "staff_invitations",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContractJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryErrorCode = table.Column<string>(type: "text", nullable: true),
                    DeliveryStatus = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    JoinDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProtectedPasswordHash = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TargetRole = table.Column<string>(type: "text", nullable: false),
                    TokenDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_invitations", x => x.Id);
                    table.CheckConstraint("ck_staff_invitation_delivery", "\"DeliveryStatus\" IN ('PENDING','SENT','FAILED')");
                    table.CheckConstraint("ck_staff_invitation_role", "\"TargetRole\" IN ('SALES','LIFEGUARD','COACH')");
                    table.CheckConstraint("ck_staff_invitation_status", "\"Status\" IN ('PENDING','COMPLETED','CANCELLED','EXPIRED')");
                    table.ForeignKey(
                        name: "FK_staff_invitations_users_CompletedUserId",
                        column: x => x.CompletedUserId,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_invitations_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_onboarding_revisions",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CccdDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CorrectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DocumentsProvided = table.Column<bool>(type: "boolean", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProtectedProfile = table.Column<string>(type: "text", nullable: false),
                    ReservedCccdDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_onboarding_revisions", x => x.Id);
                    table.CheckConstraint("ck_staff_revision_owner", "(\"InvitationId\" IS NOT NULL) <> (\"EmployeeId\" IS NOT NULL)");
                    table.CheckConstraint("ck_staff_revision_state", "\"State\" IN ('DRAFT','SUBMITTED','APPROVED','CHANGES_REQUESTED','REJECTED')");
                    table.ForeignKey(
                        name: "FK_staff_onboarding_revisions_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "smart_pool",
                        principalTable: "employees",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_onboarding_revisions_staff_invitations_InvitationId",
                        column: x => x.InvitationId,
                        principalSchema: "smart_pool",
                        principalTable: "staff_invitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_onboarding_revisions_users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_onboarding_documents",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<string>(type: "text", nullable: true),
                    AssetVersion = table.Column<string>(type: "text", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    DeleteAfterUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletionReason = table.Column<string>(type: "text", nullable: true),
                    DeletionState = table.Column<string>(type: "text", nullable: false),
                    DeliveryType = table.Column<string>(type: "text", nullable: false),
                    LastErrorCode = table.Column<string>(type: "text", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublicId = table.Column<string>(type: "text", nullable: true),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    ResourceType = table.Column<string>(type: "text", nullable: false),
                    RetryAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    UploadSettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploaderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_onboarding_documents", x => x.Id);
                    table.CheckConstraint("ck_staff_document_deadline", "\"Purpose\" = 'AVATAR' OR (\"DeleteAfterUtc\" IS NOT NULL AND \"DeleteAfterUtc\" = \"UploadedAtUtc\" + interval '30 days')");
                    table.CheckConstraint("ck_staff_document_deletion", "\"DeletionState\" IN ('UPLOADING','AVAILABLE','PENDING','DELETED')");
                    table.CheckConstraint("ck_staff_document_private", "\"Purpose\" = 'AVATAR' OR \"DeliveryType\" = 'authenticated'");
                    table.CheckConstraint("ck_staff_document_purpose", "\"Purpose\" IN ('FRONT','BACK','AVATAR')");
                    table.ForeignKey(
                        name: "FK_staff_onboarding_documents_staff_onboarding_revisions_Revis~",
                        column: x => x.RevisionId,
                        principalSchema: "smart_pool",
                        principalTable: "staff_onboarding_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employees_correction_revision_id",
                schema: "smart_pool",
                table: "employees",
                column: "correction_revision_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_employees_correction_active",
                schema: "smart_pool",
                table: "employees",
                sql: "(correction_request_id IS NULL AND correction_reason IS NULL AND correction_requested_at_utc IS NULL AND correction_revision_id IS NULL) OR (correction_request_id IS NOT NULL AND correction_reason IS NOT NULL AND btrim(correction_reason) <> '' AND correction_requested_at_utc IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_staff_invitations_CompletedUserId",
                schema: "smart_pool",
                table: "staff_invitations",
                column: "CompletedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_invitations_CreatedBy",
                schema: "smart_pool",
                table: "staff_invitations",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_staff_invitations_Email",
                schema: "smart_pool",
                table: "staff_invitations",
                column: "Email",
                unique: true,
                filter: "\"Status\" IN ('PENDING','EXPIRED')");

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_documents_DeleteAfterUtc",
                schema: "smart_pool",
                table: "staff_onboarding_documents",
                column: "DeleteAfterUtc");

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_documents_DeletionState_RetryAtUtc",
                schema: "smart_pool",
                table: "staff_onboarding_documents",
                columns: new[] { "DeletionState", "RetryAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_documents_RevisionId_Purpose",
                schema: "smart_pool",
                table: "staff_onboarding_documents",
                columns: new[] { "RevisionId", "Purpose" },
                unique: true,
                filter: "\"DeletionState\" = 'AVAILABLE'");

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_revisions_EmployeeId",
                schema: "smart_pool",
                table: "staff_onboarding_revisions",
                column: "EmployeeId",
                unique: true,
                filter: "\"State\" IN ('DRAFT','SUBMITTED','CHANGES_REQUESTED')");

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_revisions_EmployeeId_RevisionNumber",
                schema: "smart_pool",
                table: "staff_onboarding_revisions",
                columns: new[] { "EmployeeId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_revisions_InvitationId_RevisionNumber",
                schema: "smart_pool",
                table: "staff_onboarding_revisions",
                columns: new[] { "InvitationId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_revisions_ReservedCccdDigest",
                schema: "smart_pool",
                table: "staff_onboarding_revisions",
                column: "ReservedCccdDigest",
                unique: true,
                filter: "\"ReservedCccdDigest\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_revisions_ReviewerId",
                schema: "smart_pool",
                table: "staff_onboarding_revisions",
                column: "ReviewerId");

            migrationBuilder.AddForeignKey(
                name: "FK_employees_staff_onboarding_revisions_correction_revision_id",
                schema: "smart_pool",
                table: "employees",
                column: "correction_revision_id",
                principalSchema: "smart_pool",
                principalTable: "staff_onboarding_revisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
