using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "coach_applications",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    certificate_public_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    certificate_format = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("coach_applications_pkey", x => x.id);
                    table.CheckConstraint("ck_coach_applications_review_fields", "(status = 'Pending' AND reviewed_by_user_id IS NULL AND reviewed_at IS NULL) OR (status <> 'Pending' AND reviewed_by_user_id IS NOT NULL AND reviewed_at IS NOT NULL)");
                    table.CheckConstraint("ck_coach_applications_revoked_fields", "(status = 'Revoked' AND revoked_by_user_id IS NOT NULL AND revoked_at IS NOT NULL AND revoke_reason IS NOT NULL AND length(btrim(revoke_reason)) > 0) OR (status <> 'Revoked' AND revoked_by_user_id IS NULL AND revoked_at IS NULL AND revoke_reason IS NULL)");
                    table.CheckConstraint("ck_coach_applications_status", "status IN ('Pending', 'Approved', 'Rejected', 'Revoked')");
                    table.ForeignKey(
                        name: "coach_applications_reviewed_by_user_id_fkey",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "coach_applications_revoked_by_user_id_fkey",
                        column: x => x.revoked_by_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "coach_applications_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_coach_applications_reviewed_by_user_id",
                schema: "smart_pool",
                table: "coach_applications",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_coach_applications_revoked_by_user_id",
                schema: "smart_pool",
                table: "coach_applications",
                column: "revoked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_coach_applications_status_submitted_at",
                schema: "smart_pool",
                table: "coach_applications",
                columns: new[] { "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ux_coach_applications_user_pending",
                schema: "smart_pool",
                table: "coach_applications",
                column: "user_id",
                unique: true,
                filter: "status = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coach_applications",
                schema: "smart_pool");
        }
    }
}
