using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffInvitationOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE UNIQUE INDEX users_normalized_email_onboarding_key ON smart_pool.users (lower(btrim(email))) WHERE email IS NOT NULL;");
            migrationBuilder.CreateTable(
                name: "staff_identity_claims",
                schema: "smart_pool",
                columns: table => new
                {
                    Digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_identity_claims", x => x.Digest);
                });

            migrationBuilder.CreateTable(
                name: "staff_invitations",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TargetRole = table.Column<string>(type: "text", nullable: false),
                    JoinDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TokenDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DeliveryStatus = table.Column<string>(type: "text", nullable: false),
                    DeliveryErrorCode = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedPasswordHash = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedUserId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    ProtectedProfile = table.Column<string>(type: "text", nullable: false),
                    CccdDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DocumentsProvided = table.Column<bool>(type: "boolean", nullable: false),
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
                name: "staff_correction_requests",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_correction_requests", x => x.Id);
                    table.CheckConstraint("ck_staff_correction_state", "\"State\" IN ('REQUESTED','OPENED','CLOSED')");
                    table.ForeignKey(
                        name: "FK_staff_correction_requests_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "smart_pool",
                        principalTable: "employees",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_correction_requests_staff_onboarding_revisions_Revisi~",
                        column: x => x.RevisionId,
                        principalSchema: "smart_pool",
                        principalTable: "staff_onboarding_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_onboarding_documents",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    AssetId = table.Column<string>(type: "text", nullable: true),
                    PublicId = table.Column<string>(type: "text", nullable: true),
                    AssetVersion = table.Column<string>(type: "text", nullable: true),
                    ResourceType = table.Column<string>(type: "text", nullable: false),
                    DeliveryType = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    UploaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadSettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeleteAfterUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletionState = table.Column<string>(type: "text", nullable: false),
                    DeletionReason = table.Column<string>(type: "text", nullable: true),
                    RetryAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "text", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "IX_staff_correction_requests_EmployeeId",
                schema: "smart_pool",
                table: "staff_correction_requests",
                column: "EmployeeId",
                unique: true,
                filter: "\"State\" IN ('REQUESTED','OPENED')");

            migrationBuilder.CreateIndex(
                name: "IX_staff_correction_requests_RevisionId",
                schema: "smart_pool",
                table: "staff_correction_requests",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_identity_claims_OwnerId",
                schema: "smart_pool",
                table: "staff_identity_claims",
                column: "OwnerId");

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
                name: "IX_staff_onboarding_revisions_ReviewerId",
                schema: "smart_pool",
                table: "staff_onboarding_revisions",
                column: "ReviewerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX smart_pool.users_normalized_email_onboarding_key;");
            migrationBuilder.DropTable(
                name: "staff_correction_requests",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "staff_identity_claims",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "staff_onboarding_documents",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "staff_onboarding_revisions",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "staff_invitations",
                schema: "smart_pool");
        }
    }
}
