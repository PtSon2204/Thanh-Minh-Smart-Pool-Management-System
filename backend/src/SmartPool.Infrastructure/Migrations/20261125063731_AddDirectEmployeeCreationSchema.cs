using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectEmployeeCreationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_schedule_change_destination",
                schema: "smart_pool",
                table: "staff_schedule_changes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_schedule_change_instruction",
                schema: "smart_pool",
                table: "staff_schedule_changes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_schedule_change_receipt",
                schema: "smart_pool",
                table: "staff_schedule_changes");

            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                schema: "smart_pool",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "temporary_password_expires_at_utc",
                schema: "smart_pool",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gender",
                schema: "smart_pool",
                table: "user_profiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "company_email",
                schema: "smart_pool",
                table: "employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "staff_legal_profiles",
                schema: "smart_pool",
                columns: table => new
                {
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    protected_profile = table.Column<string>(type: "text", nullable: false),
                    cccd_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("staff_legal_profiles_pkey", x => x.employee_id);
                    table.ForeignKey(
                        name: "staff_legal_profiles_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "smart_pool",
                        principalTable: "employees",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_schedule_change_destination",
                schema: "smart_pool",
                table: "staff_schedule_changes",
                sql: "\"AppliedVersion\" IS NULL OR \"Kind\" = 'Pattern'\nOR (\"Kind\" = 'Leave' AND \"DestinationShiftId\" IS NULL AND \"DestinationEmployeeId\" IS NULL)\nOR (\"Kind\" = 'Move' AND \"DestinationShiftId\" IS NOT NULL AND \"DestinationEmployeeId\" IS NULL\n    AND jsonb_array_length(\"SourceAssignmentIdsJson\") = 1)\nOR (\"Kind\" = 'Replace' AND \"DestinationShiftId\" IS NOT NULL AND \"DestinationEmployeeId\" IS NOT NULL\n    AND \"DestinationEmployeeId\" <> \"EmployeeId\" AND jsonb_array_length(\"SourceAssignmentIdsJson\") = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_schedule_change_instruction",
                schema: "smart_pool",
                table: "staff_schedule_changes",
                sql: "(\"AppliedVersion\" IS NULL AND \"OperationId\" IS NOT NULL)\nOR (\"Kind\" = 'Pattern' AND \"ContractJson\" IS NOT NULL AND jsonb_typeof(\"ContractJson\") = 'object'\n    AND \"EffectiveFrom\" IS NOT NULL AND \"WorkDate\" IS NULL AND \"SourceAssignmentIdsJson\" IS NULL\n    AND \"DestinationShiftId\" IS NULL AND \"DestinationEmployeeId\" IS NULL AND \"Reason\" IS NULL)\nOR (\"Kind\" IN ('Leave','Move','Replace') AND \"ContractJson\" IS NULL AND \"EffectiveFrom\" IS NULL\n    AND \"WorkDate\" IS NOT NULL AND \"SourceAssignmentIdsJson\" IS NOT NULL\n    AND jsonb_typeof(\"SourceAssignmentIdsJson\") = 'array' AND \"Reason\" IS NOT NULL AND btrim(\"Reason\") <> '')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_schedule_change_receipt",
                schema: "smart_pool",
                table: "staff_schedule_changes",
                sql: "(\"OperationId\" IS NULL AND \"ProposalHash\" IS NULL AND \"ResponseJson\" IS NULL)\nOR (\"OperationId\" IS NOT NULL AND \"ProposalHash\" IS NOT NULL AND \"ResponseJson\" IS NOT NULL\n    AND \"ProposalHash\" ~ '^[0-9A-Fa-f]{64}$' AND jsonb_typeof(\"ResponseJson\") = 'object')");

            migrationBuilder.CreateIndex(
                name: "employees_company_email_key",
                schema: "smart_pool",
                table: "employees",
                column: "company_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "staff_legal_profiles_cccd_digest_key",
                schema: "smart_pool",
                table: "staff_legal_profiles",
                column: "cccd_digest",
                unique: true);

            // Backfill one legal profile per employee from the latest approved onboarding
            // revision. Invitation-linked revisions have no EmployeeId, so the owner is
            // resolved through the invitation's CompletedUserId (plan §2.3).
            migrationBuilder.Sql(@"DO $$
DECLARE tie_count int;
BEGIN
    SELECT count(*) INTO tie_count FROM (
        SELECT c.owner_id
        FROM (
            SELECT COALESCE(r.""EmployeeId"", i.""CompletedUserId"") AS owner_id,
                   (r.""EmployeeId"" IS NOT NULL) AS employee_linked,
                   r.""RevisionNumber""
            FROM smart_pool.staff_onboarding_revisions r
            LEFT JOIN smart_pool.staff_invitations i ON i.""Id"" = r.""InvitationId""
            WHERE r.""State"" = 'APPROVED' AND COALESCE(r.""EmployeeId"", i.""CompletedUserId"") IS NOT NULL
        ) c
        GROUP BY c.owner_id, c.employee_linked, c.""RevisionNumber""
        HAVING count(*) > 1
    ) t;
    IF tie_count > 0 THEN
        RAISE EXCEPTION 'AddDirectEmployeeCreationSchema: unresolved ties among approved revisions for % owner(s)', tie_count;
    END IF;
END $$;");

            migrationBuilder.Sql(@"INSERT INTO smart_pool.staff_legal_profiles (employee_id, protected_profile, cccd_digest, version, created_at_utc, updated_at_utc)
SELECT picked.owner_id, picked.protected_profile, picked.cccd_digest, 1, picked.created_at_utc, picked.reviewed_at_utc
FROM (
    SELECT COALESCE(r.""EmployeeId"", i.""CompletedUserId"") AS owner_id,
           r.""ProtectedProfile"" AS protected_profile,
           r.""CccdDigest"" AS cccd_digest,
           r.""CreatedAtUtc"" AS created_at_utc,
           r.""ReviewedAtUtc"" AS reviewed_at_utc,
           ROW_NUMBER() OVER (
               PARTITION BY COALESCE(r.""EmployeeId"", i.""CompletedUserId"")
               ORDER BY (r.""EmployeeId"" IS NOT NULL) DESC, r.""RevisionNumber"" DESC
           ) AS rn
    FROM smart_pool.staff_onboarding_revisions r
    LEFT JOIN smart_pool.staff_invitations i ON i.""Id"" = r.""InvitationId""
    WHERE r.""State"" = 'APPROVED' AND COALESCE(r.""EmployeeId"", i.""CompletedUserId"") IS NOT NULL
) picked
WHERE picked.rn = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_legal_profiles",
                schema: "smart_pool");

            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_schedule_change_destination",
                schema: "smart_pool",
                table: "staff_schedule_changes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_schedule_change_instruction",
                schema: "smart_pool",
                table: "staff_schedule_changes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_schedule_change_receipt",
                schema: "smart_pool",
                table: "staff_schedule_changes");

            migrationBuilder.DropIndex(
                name: "employees_company_email_key",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "must_change_password",
                schema: "smart_pool",
                table: "users");

            migrationBuilder.DropColumn(
                name: "temporary_password_expires_at_utc",
                schema: "smart_pool",
                table: "users");

            migrationBuilder.DropColumn(
                name: "gender",
                schema: "smart_pool",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "company_email",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_schedule_change_destination",
                schema: "smart_pool",
                table: "staff_schedule_changes",
                sql: "\"AppliedVersion\" IS NULL OR \"Kind\" = 'Pattern'\r\nOR (\"Kind\" = 'Leave' AND \"DestinationShiftId\" IS NULL AND \"DestinationEmployeeId\" IS NULL)\r\nOR (\"Kind\" = 'Move' AND \"DestinationShiftId\" IS NOT NULL AND \"DestinationEmployeeId\" IS NULL\r\n    AND jsonb_array_length(\"SourceAssignmentIdsJson\") = 1)\r\nOR (\"Kind\" = 'Replace' AND \"DestinationShiftId\" IS NOT NULL AND \"DestinationEmployeeId\" IS NOT NULL\r\n    AND \"DestinationEmployeeId\" <> \"EmployeeId\" AND jsonb_array_length(\"SourceAssignmentIdsJson\") = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_schedule_change_instruction",
                schema: "smart_pool",
                table: "staff_schedule_changes",
                sql: "(\"AppliedVersion\" IS NULL AND \"OperationId\" IS NOT NULL)\r\nOR (\"Kind\" = 'Pattern' AND \"ContractJson\" IS NOT NULL AND jsonb_typeof(\"ContractJson\") = 'object'\r\n    AND \"EffectiveFrom\" IS NOT NULL AND \"WorkDate\" IS NULL AND \"SourceAssignmentIdsJson\" IS NULL\r\n    AND \"DestinationShiftId\" IS NULL AND \"DestinationEmployeeId\" IS NULL AND \"Reason\" IS NULL)\r\nOR (\"Kind\" IN ('Leave','Move','Replace') AND \"ContractJson\" IS NULL AND \"EffectiveFrom\" IS NULL\r\n    AND \"WorkDate\" IS NOT NULL AND \"SourceAssignmentIdsJson\" IS NOT NULL\r\n    AND jsonb_typeof(\"SourceAssignmentIdsJson\") = 'array' AND \"Reason\" IS NOT NULL AND btrim(\"Reason\") <> '')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_schedule_change_receipt",
                schema: "smart_pool",
                table: "staff_schedule_changes",
                sql: "(\"OperationId\" IS NULL AND \"ProposalHash\" IS NULL AND \"ResponseJson\" IS NULL)\r\nOR (\"OperationId\" IS NOT NULL AND \"ProposalHash\" IS NOT NULL AND \"ResponseJson\" IS NOT NULL\r\n    AND \"ProposalHash\" ~ '^[0-9A-Fa-f]{64}$' AND jsonb_typeof(\"ResponseJson\") = 'object')");
        }
    }
}
