using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyStaffPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReservedCccdDigest",
                schema: "smart_pool",
                table: "staff_onboarding_revisions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "daily_inspection_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_executed_at_utc",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_executed_by",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_execution_assignee_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "last_execution_was_admin_override",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "boolean",
                nullable: true);

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
                name: "staff_schedule_changes",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AppliedVersion = table.Column<long>(type: "bigint", nullable: true),
                    ContractJson = table.Column<string>(type: "jsonb", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SourceAssignmentIdsJson = table.Column<string>(type: "jsonb", nullable: true),
                    DestinationShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposalHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_schedule_changes", x => x.Id);
                    table.CheckConstraint("ck_staff_schedule_change_applied_version", "\"AppliedVersion\" IS NULL OR \"AppliedVersion\" > 0");
                    table.CheckConstraint("ck_staff_schedule_change_destination", "\"AppliedVersion\" IS NULL OR \"Kind\" = 'Pattern'\nOR (\"Kind\" = 'Leave' AND \"DestinationShiftId\" IS NULL AND \"DestinationEmployeeId\" IS NULL)\nOR (\"Kind\" = 'Move' AND \"DestinationShiftId\" IS NOT NULL AND \"DestinationEmployeeId\" IS NULL\n    AND jsonb_array_length(\"SourceAssignmentIdsJson\") = 1)\nOR (\"Kind\" = 'Replace' AND \"DestinationShiftId\" IS NOT NULL AND \"DestinationEmployeeId\" IS NOT NULL\n    AND \"DestinationEmployeeId\" <> \"EmployeeId\" AND jsonb_array_length(\"SourceAssignmentIdsJson\") = 1)");
                    table.CheckConstraint("ck_staff_schedule_change_instruction", "(\"AppliedVersion\" IS NULL AND \"OperationId\" IS NOT NULL)\nOR (\"Kind\" = 'Pattern' AND \"ContractJson\" IS NOT NULL AND jsonb_typeof(\"ContractJson\") = 'object'\n    AND \"EffectiveFrom\" IS NOT NULL AND \"WorkDate\" IS NULL AND \"SourceAssignmentIdsJson\" IS NULL\n    AND \"DestinationShiftId\" IS NULL AND \"DestinationEmployeeId\" IS NULL AND \"Reason\" IS NULL)\nOR (\"Kind\" IN ('Leave','Move','Replace') AND \"ContractJson\" IS NULL AND \"EffectiveFrom\" IS NULL\n    AND \"WorkDate\" IS NOT NULL AND \"SourceAssignmentIdsJson\" IS NOT NULL\n    AND jsonb_typeof(\"SourceAssignmentIdsJson\") = 'array' AND \"Reason\" IS NOT NULL AND btrim(\"Reason\") <> '')");
                    table.CheckConstraint("ck_staff_schedule_change_kind", "\"Kind\" IN ('Pattern','Leave','Move','Replace')");
                    table.CheckConstraint("ck_staff_schedule_change_outcome", "\"AppliedVersion\" IS NOT NULL OR \"OperationId\" IS NOT NULL");
                    table.CheckConstraint("ck_staff_schedule_change_receipt", "(\"OperationId\" IS NULL AND \"ProposalHash\" IS NULL AND \"ResponseJson\" IS NULL)\nOR (\"OperationId\" IS NOT NULL AND \"ProposalHash\" IS NOT NULL AND \"ResponseJson\" IS NOT NULL\n    AND \"ProposalHash\" ~ '^[0-9A-Fa-f]{64}$' AND jsonb_typeof(\"ResponseJson\") = 'object')");
                    table.ForeignKey(
                        name: "FK_staff_schedule_changes_employees_DestinationEmployeeId",
                        column: x => x.DestinationEmployeeId,
                        principalSchema: "smart_pool",
                        principalTable: "employees",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_schedule_changes_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "smart_pool",
                        principalTable: "employees",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_schedule_changes_shifts_DestinationShiftId",
                        column: x => x.DestinationShiftId,
                        principalSchema: "smart_pool",
                        principalTable: "shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_schedule_changes_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_onboarding_revisions_ReservedCccdDigest",
                schema: "smart_pool",
                table: "staff_onboarding_revisions",
                column: "ReservedCccdDigest",
                unique: true,
                filter: "\"ReservedCccdDigest\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_pool_duty_schedules_daily_inspection_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "daily_inspection_id");

            migrationBuilder.CreateIndex(
                name: "IX_pool_duty_schedules_last_executed_by",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "last_executed_by");

            migrationBuilder.CreateIndex(
                name: "IX_pool_duty_schedules_last_execution_assignee_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "last_execution_assignee_id");

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
                name: "IX_staff_schedule_changes_CreatedBy",
                schema: "smart_pool",
                table: "staff_schedule_changes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_changes_DestinationEmployeeId",
                schema: "smart_pool",
                table: "staff_schedule_changes",
                column: "DestinationEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_changes_DestinationShiftId",
                schema: "smart_pool",
                table: "staff_schedule_changes",
                column: "DestinationShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_changes_EmployeeId_AppliedVersion",
                schema: "smart_pool",
                table: "staff_schedule_changes",
                columns: new[] { "EmployeeId", "AppliedVersion" },
                unique: true,
                filter: "\"AppliedVersion\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_changes_EmployeeId_WorkDate",
                schema: "smart_pool",
                table: "staff_schedule_changes",
                columns: new[] { "EmployeeId", "WorkDate" },
                filter: "\"AppliedVersion\" IS NOT NULL AND \"WorkDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_changes_OperationId",
                schema: "smart_pool",
                table: "staff_schedule_changes",
                column: "OperationId",
                unique: true,
                filter: "\"OperationId\" IS NOT NULL");

            migrationBuilder.DropForeignKey(
                name: "FK_employee_shifts_staff_schedule_exceptions_schedule_exceptio~",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.DropForeignKey(
                name: "FK_employee_shifts_staff_work_patterns_work_pattern_id",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.Sql("""
                LOCK TABLE smart_pool.pool_duty_schedules, smart_pool.pool_duty_task_executions,
                    smart_pool.staff_work_patterns, smart_pool.staff_schedule_exceptions,
                    smart_pool.staff_correction_requests, smart_pool.staff_identity_claims,
                    smart_pool.staff_onboarding_revisions, smart_pool.staff_invitations,
                    smart_pool.employee_shifts IN ACCESS EXCLUSIVE MODE;

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM smart_pool.employee_shifts WHERE work_pattern_id IS NOT NULL AND schedule_exception_id IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot simplify staff persistence: an assignment has two legacy provenance links.';
                    END IF;
                    IF EXISTS (
                        SELECT "Id" FROM smart_pool.staff_work_patterns
                        INTERSECT SELECT "Id" FROM smart_pool.staff_schedule_exceptions
                    ) OR EXISTS (
                        SELECT "EmployeeId", "Version" FROM smart_pool.staff_work_patterns
                        INTERSECT SELECT "EmployeeId", "Version" FROM smart_pool.staff_schedule_exceptions
                    ) THEN
                        RAISE EXCEPTION 'Cannot simplify staff persistence: legacy schedule identifiers or applied versions collide.';
                    END IF;
                    IF EXISTS (
                        WITH ranked AS (
                            SELECT operational_task_id, water_quality_log_id, daily_inspection_id,
                                row_number() OVER (PARTITION BY operational_task_id ORDER BY task_revision DESC, executed_at_utc DESC, id DESC) AS row_number
                            FROM smart_pool.pool_duty_task_executions
                        )
                        SELECT 1 FROM ranked execution
                        JOIN smart_pool.pool_duty_schedules task ON task.id = execution.operational_task_id
                        WHERE execution.row_number = 1 AND task.water_quality_log_id IS NOT NULL
                            AND execution.water_quality_log_id IS NOT NULL AND task.water_quality_log_id <> execution.water_quality_log_id
                    ) THEN
                        RAISE EXCEPTION 'Cannot simplify staff persistence: latest execution conflicts with the current task result.';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM smart_pool.staff_correction_requests request
                        LEFT JOIN smart_pool.staff_onboarding_revisions revision ON revision."Id" = request."RevisionId"
                        WHERE request."State" IN ('REQUESTED','OPENED')
                            AND (request."State" = 'OPENED' AND (revision."Id" IS NULL OR revision."EmployeeId" <> request."EmployeeId"))
                    ) THEN
                        RAISE EXCEPTION 'Cannot simplify staff persistence: an active correction request has an invalid revision owner.';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM smart_pool.staff_identity_claims claim
                        WHERE NOT EXISTS (
                            SELECT 1 FROM smart_pool.staff_onboarding_revisions revision
                            LEFT JOIN smart_pool.staff_invitations invitation ON invitation."Id" = revision."InvitationId"
                            WHERE revision."CccdDigest" = claim."Digest"
                                AND COALESCE(revision."EmployeeId", invitation."CompletedUserId", invitation."Id") = claim."OwnerId"
                        )
                    ) THEN
                        RAISE EXCEPTION 'Cannot simplify staff persistence: an identity claim has no owner-compatible revision.';
                    END IF;
                END $$;

                WITH ranked AS (
                    SELECT *, row_number() OVER (PARTITION BY operational_task_id ORDER BY task_revision DESC, executed_at_utc DESC, id DESC) AS row_number
                    FROM smart_pool.pool_duty_task_executions
                )
                UPDATE smart_pool.pool_duty_schedules task
                SET last_executed_by = execution.actor_id,
                    last_executed_at_utc = execution.executed_at_utc,
                    last_execution_assignee_id = execution.original_assignee_id,
                    last_execution_was_admin_override = execution.is_admin_override,
                    daily_inspection_id = execution.daily_inspection_id,
                    water_quality_log_id = COALESCE(task.water_quality_log_id, execution.water_quality_log_id)
                FROM ranked execution
                WHERE execution.row_number = 1 AND task.id = execution.operational_task_id;

                INSERT INTO smart_pool.staff_schedule_changes ("Id", "EmployeeId", "Kind", "AppliedVersion", "ContractJson", "EffectiveFrom", "CreatedBy", "CreatedAtUtc")
                SELECT "Id", "EmployeeId", 'Pattern', "Version", "ContractJson"::jsonb, "EffectiveFrom", "CreatedBy", "CreatedAtUtc"
                FROM smart_pool.staff_work_patterns;

                INSERT INTO smart_pool.staff_schedule_changes ("Id", "EmployeeId", "Kind", "AppliedVersion", "WorkDate", "SourceAssignmentIdsJson", "DestinationShiftId", "DestinationEmployeeId", "Reason", "CreatedBy", "CreatedAtUtc")
                SELECT "Id", "EmployeeId", "Kind", "Version", "WorkDate", "SourceAssignmentIdsJson"::jsonb, "DestinationShiftId", "DestinationEmployeeId", "Reason", "CreatedBy", "CreatedAtUtc"
                FROM smart_pool.staff_schedule_exceptions;

                UPDATE smart_pool.employees employee
                SET correction_request_id = request."Id",
                    correction_reason = request."Reason",
                    correction_requested_at_utc = request."CreatedAtUtc",
                    correction_revision_id = request."RevisionId",
                    correction_version = request."Version"
                FROM smart_pool.staff_correction_requests request
                WHERE request."EmployeeId" = employee.user_id AND request."State" IN ('REQUESTED','OPENED');

                WITH ranked AS (
                    SELECT claim."Digest", revision."Id",
                        row_number() OVER (PARTITION BY claim."Digest" ORDER BY
                            (revision."State" = 'APPROVED') DESC,
                            (revision."State" IN ('SUBMITTED','CHANGES_REQUESTED')) DESC,
                            revision."RevisionNumber" DESC, revision."CreatedAtUtc" DESC) AS row_number
                    FROM smart_pool.staff_identity_claims claim
                    JOIN smart_pool.staff_onboarding_revisions revision ON revision."CccdDigest" = claim."Digest"
                    LEFT JOIN smart_pool.staff_invitations invitation ON invitation."Id" = revision."InvitationId"
                    WHERE COALESCE(revision."EmployeeId", invitation."CompletedUserId", invitation."Id") = claim."OwnerId"
                )
                UPDATE smart_pool.staff_onboarding_revisions revision
                SET "ReservedCccdDigest" = ranked."Digest"
                FROM ranked
                WHERE ranked.row_number = 1 AND revision."Id" = ranked."Id";

                UPDATE smart_pool.employee_shifts assignment
                SET work_pattern_id = exception."Id"
                FROM smart_pool.staff_schedule_exceptions exception
                WHERE assignment.schedule_exception_id = exception."Id";
                """);

            migrationBuilder.DropIndex(
                name: "IX_employee_shifts_schedule_exception_id",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.DropColumn(
                name: "schedule_exception_id",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.RenameColumn(
                name: "work_pattern_id",
                schema: "smart_pool",
                table: "employee_shifts",
                newName: "schedule_change_id");

            migrationBuilder.RenameIndex(
                name: "IX_employee_shifts_work_pattern_id",
                schema: "smart_pool",
                table: "employee_shifts",
                newName: "IX_employee_shifts_schedule_change_id");

            migrationBuilder.AddForeignKey(
                name: "FK_employee_shifts_staff_schedule_changes_schedule_change_id",
                schema: "smart_pool",
                table: "employee_shifts",
                column: "schedule_change_id",
                principalSchema: "smart_pool",
                principalTable: "staff_schedule_changes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employees_staff_onboarding_revisions_correction_revision_id",
                schema: "smart_pool",
                table: "employees",
                column: "correction_revision_id",
                principalSchema: "smart_pool",
                principalTable: "staff_onboarding_revisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "pool_duty_schedules_daily_inspection_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "daily_inspection_id",
                principalSchema: "smart_pool",
                principalTable: "daily_inspections",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "pool_duty_schedules_last_executed_by_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "last_executed_by",
                principalSchema: "smart_pool",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "pool_duty_schedules_last_execution_assignee_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "last_execution_assignee_id",
                principalSchema: "smart_pool",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "pool_duty_task_executions",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "staff_correction_requests",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "staff_identity_claims",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "staff_schedule_exceptions",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "staff_schedule_operations",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "staff_schedule_previews",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "staff_work_patterns",
                schema: "smart_pool");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_employee_shifts_staff_schedule_changes_schedule_change_id",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.DropForeignKey(
                name: "FK_employees_staff_onboarding_revisions_correction_revision_id",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.DropForeignKey(
                name: "pool_duty_schedules_daily_inspection_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropForeignKey(
                name: "pool_duty_schedules_last_executed_by_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropForeignKey(
                name: "pool_duty_schedules_last_execution_assignee_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropTable(
                name: "staff_schedule_changes",
                schema: "smart_pool");

            migrationBuilder.DropIndex(
                name: "IX_staff_onboarding_revisions_ReservedCccdDigest",
                schema: "smart_pool",
                table: "staff_onboarding_revisions");

            migrationBuilder.DropIndex(
                name: "IX_pool_duty_schedules_daily_inspection_id",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropIndex(
                name: "IX_pool_duty_schedules_last_executed_by",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropIndex(
                name: "IX_pool_duty_schedules_last_execution_assignee_id",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropIndex(
                name: "IX_employees_correction_revision_id",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.DropCheckConstraint(
                name: "ck_employees_correction_active",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "ReservedCccdDigest",
                schema: "smart_pool",
                table: "staff_onboarding_revisions");

            migrationBuilder.DropColumn(
                name: "daily_inspection_id",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "last_executed_at_utc",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "last_executed_by",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "last_execution_assignee_id",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "last_execution_was_admin_override",
                schema: "smart_pool",
                table: "pool_duty_schedules");

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

            migrationBuilder.RenameColumn(
                name: "schedule_change_id",
                schema: "smart_pool",
                table: "employee_shifts",
                newName: "work_pattern_id");

            migrationBuilder.RenameIndex(
                name: "IX_employee_shifts_schedule_change_id",
                schema: "smart_pool",
                table: "employee_shifts",
                newName: "IX_employee_shifts_work_pattern_id");

            migrationBuilder.AddColumn<Guid>(
                name: "schedule_exception_id",
                schema: "smart_pool",
                table: "employee_shifts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pool_duty_task_executions",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    daily_inspection_id = table.Column<Guid>(type: "uuid", nullable: true),
                    executed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_admin_override = table.Column<bool>(type: "boolean", nullable: false),
                    operational_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    result_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    task_revision = table.Column<long>(type: "bigint", nullable: false),
                    water_quality_log_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pool_duty_task_executions_pkey", x => x.id);
                    table.CheckConstraint("ck_pool_duty_task_executions_result", "(water_quality_log_id IS NOT NULL AND daily_inspection_id IS NULL) OR (water_quality_log_id IS NULL AND daily_inspection_id IS NOT NULL)");
                    table.CheckConstraint("ck_pool_duty_task_executions_result_status", "(water_quality_log_id IS NOT NULL AND result_status IN ('NotEvaluated', 'Incomplete', 'WithinLimits', 'Warning', 'Danger')) OR (daily_inspection_id IS NOT NULL AND result_status IN ('Passed', 'HasIssues'))");
                    table.CheckConstraint("ck_pool_duty_task_executions_revision", "task_revision > 0");
                    table.ForeignKey(
                        name: "FK_pool_duty_task_executions_daily_inspections_daily_inspectio~",
                        column: x => x.daily_inspection_id,
                        principalSchema: "smart_pool",
                        principalTable: "daily_inspections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pool_duty_task_executions_pool_duty_schedules_operational_t~",
                        column: x => x.operational_task_id,
                        principalSchema: "smart_pool",
                        principalTable: "pool_duty_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pool_duty_task_executions_users_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pool_duty_task_executions_users_original_assignee_id",
                        column: x => x.original_assignee_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pool_duty_task_executions_water_quality_logs_water_quality_~",
                        column: x => x.water_quality_log_id,
                        principalSchema: "smart_pool",
                        principalTable: "water_quality_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_correction_requests",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
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
                name: "staff_schedule_exceptions",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceAssignmentIdsJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_schedule_exceptions", x => x.Id);
                    table.CheckConstraint("ck_schedule_exception_kind", "\"Kind\" IN ('Leave','Move','Replace')");
                    table.ForeignKey(
                        name: "FK_staff_schedule_exceptions_employees_DestinationEmployeeId",
                        column: x => x.DestinationEmployeeId,
                        principalSchema: "smart_pool",
                        principalTable: "employees",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_schedule_exceptions_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "smart_pool",
                        principalTable: "employees",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_schedule_exceptions_shifts_DestinationShiftId",
                        column: x => x.DestinationShiftId,
                        principalSchema: "smart_pool",
                        principalTable: "shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_schedule_exceptions_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_schedule_operations",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_schedule_operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_schedule_operations_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "smart_pool",
                        principalTable: "employees",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_schedule_previews",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProposalHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StateHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_schedule_previews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_schedule_previews_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "smart_pool",
                        principalTable: "employees",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_work_patterns",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_work_patterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_work_patterns_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "smart_pool",
                        principalTable: "employees",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_work_patterns_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employee_shifts_schedule_exception_id",
                schema: "smart_pool",
                table: "employee_shifts",
                column: "schedule_exception_id");

            migrationBuilder.CreateIndex(
                name: "IX_pool_duty_task_executions_actor_id",
                schema: "smart_pool",
                table: "pool_duty_task_executions",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_pool_duty_task_executions_original_assignee_id",
                schema: "smart_pool",
                table: "pool_duty_task_executions",
                column: "original_assignee_id");

            migrationBuilder.CreateIndex(
                name: "ux_pool_duty_task_executions_inspection_result",
                schema: "smart_pool",
                table: "pool_duty_task_executions",
                column: "daily_inspection_id",
                unique: true,
                filter: "daily_inspection_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_pool_duty_task_executions_task_revision",
                schema: "smart_pool",
                table: "pool_duty_task_executions",
                columns: new[] { "operational_task_id", "task_revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pool_duty_task_executions_water_result",
                schema: "smart_pool",
                table: "pool_duty_task_executions",
                column: "water_quality_log_id",
                unique: true,
                filter: "water_quality_log_id IS NOT NULL");

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
                name: "IX_staff_schedule_exceptions_CreatedBy",
                schema: "smart_pool",
                table: "staff_schedule_exceptions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_exceptions_DestinationEmployeeId",
                schema: "smart_pool",
                table: "staff_schedule_exceptions",
                column: "DestinationEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_exceptions_DestinationShiftId",
                schema: "smart_pool",
                table: "staff_schedule_exceptions",
                column: "DestinationShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_exceptions_EmployeeId_Version",
                schema: "smart_pool",
                table: "staff_schedule_exceptions",
                columns: new[] { "EmployeeId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_exceptions_EmployeeId_WorkDate",
                schema: "smart_pool",
                table: "staff_schedule_exceptions",
                columns: new[] { "EmployeeId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_operations_EmployeeId_CreatedAtUtc",
                schema: "smart_pool",
                table: "staff_schedule_operations",
                columns: new[] { "EmployeeId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_previews_EmployeeId",
                schema: "smart_pool",
                table: "staff_schedule_previews",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_work_patterns_CreatedBy",
                schema: "smart_pool",
                table: "staff_work_patterns",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_staff_work_patterns_EmployeeId_Version",
                schema: "smart_pool",
                table: "staff_work_patterns",
                columns: new[] { "EmployeeId", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_employee_shifts_staff_schedule_exceptions_schedule_exceptio~",
                schema: "smart_pool",
                table: "employee_shifts",
                column: "schedule_exception_id",
                principalSchema: "smart_pool",
                principalTable: "staff_schedule_exceptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employee_shifts_staff_work_patterns_work_pattern_id",
                schema: "smart_pool",
                table: "employee_shifts",
                column: "work_pattern_id",
                principalSchema: "smart_pool",
                principalTable: "staff_work_patterns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
