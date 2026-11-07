using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffContractDrivenSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Older installations can lack the duty table although it exists in the model snapshot.
            // Preserve existing tables and create the prerequisite only when it is absent.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS smart_pool.pool_duty_schedules (
                    id uuid NOT NULL DEFAULT uuid_generate_v4(),
                    duty_date date NOT NULL,
                    shift_id uuid NOT NULL,
                    assigned_employee_id uuid NOT NULL,
                    status character varying(20) NOT NULL DEFAULT 'Scheduled',
                    assignment_type character varying(20) NOT NULL DEFAULT 'Auto',
                    water_quality_log_id uuid NULL,
                    notes character varying(500) NULL,
                    created_at timestamp with time zone NOT NULL DEFAULT now(),
                    updated_at timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT pool_duty_schedules_pkey PRIMARY KEY (id),
                    CONSTRAINT ck_pool_duty_schedules_status
                        CHECK (status IN ('Scheduled', 'InProgress', 'Completed', 'Absent', 'Cancelled')),
                    CONSTRAINT ck_pool_duty_schedules_assignment_type CHECK (assignment_type IN ('Auto', 'Manual')),
                    CONSTRAINT pool_duty_schedules_shift_id_fkey
                        FOREIGN KEY (shift_id) REFERENCES smart_pool.shifts (id) ON DELETE RESTRICT,
                    CONSTRAINT pool_duty_schedules_assigned_employee_id_fkey
                        FOREIGN KEY (assigned_employee_id) REFERENCES smart_pool.users (id) ON DELETE RESTRICT,
                    CONSTRAINT pool_duty_schedules_water_quality_log_id_fkey
                        FOREIGN KEY (water_quality_log_id) REFERENCES smart_pool.water_quality_logs (id) ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS ix_pool_duty_schedules_date_shift
                    ON smart_pool.pool_duty_schedules (duty_date, shift_id);
                CREATE INDEX IF NOT EXISTS "IX_pool_duty_schedules_assigned_employee_id"
                    ON smart_pool.pool_duty_schedules (assigned_employee_id);
                CREATE INDEX IF NOT EXISTS "IX_pool_duty_schedules_shift_id"
                    ON smart_pool.pool_duty_schedules (shift_id);
                CREATE INDEX IF NOT EXISTS "IX_pool_duty_schedules_water_quality_log_id"
                    ON smart_pool.pool_duty_schedules (water_quality_log_id);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE smart_pool.pool_duty_schedules
                    DROP CONSTRAINT IF EXISTS ck_pool_duty_schedules_status;
                """);

            migrationBuilder.AddColumn<string>(
                name: "ContractJson",
                schema: "smart_pool",
                table: "staff_invitations",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "assigned_employee_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "schedule_exception_id",
                schema: "smart_pool",
                table: "employee_shifts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "work_pattern_id",
                schema: "smart_pool",
                table: "employee_shifts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "staff_schedule_exceptions",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceAssignmentIdsJson = table.Column<string>(type: "text", nullable: false),
                    DestinationShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_schedule_exceptions", x => x.Id);
                    table.CheckConstraint("ck_schedule_exception_kind", "\"Kind\" IN ('Leave','Move')");
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
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StateHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractJson = table.Column<string>(type: "text", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.AddCheckConstraint(
                name: "ck_pool_duty_schedules_status",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                sql: "status IN ('Scheduled', 'InProgress', 'Completed', 'Absent', 'Cancelled', 'Unassigned')");

            migrationBuilder.CreateIndex(
                name: "IX_employee_shifts_schedule_exception_id",
                schema: "smart_pool",
                table: "employee_shifts",
                column: "schedule_exception_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_shifts_work_pattern_id",
                schema: "smart_pool",
                table: "employee_shifts",
                column: "work_pattern_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_exceptions_CreatedBy",
                schema: "smart_pool",
                table: "staff_schedule_exceptions",
                column: "CreatedBy");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_employee_shifts_staff_schedule_exceptions_schedule_exceptio~",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.DropForeignKey(
                name: "FK_employee_shifts_staff_work_patterns_work_pattern_id",
                schema: "smart_pool",
                table: "employee_shifts");

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

            migrationBuilder.DropCheckConstraint(
                name: "ck_pool_duty_schedules_status",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropIndex(
                name: "IX_employee_shifts_schedule_exception_id",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.DropIndex(
                name: "IX_employee_shifts_work_pattern_id",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.DropColumn(
                name: "ContractJson",
                schema: "smart_pool",
                table: "staff_invitations");

            migrationBuilder.DropColumn(
                name: "schedule_exception_id",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.DropColumn(
                name: "work_pattern_id",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.AlterColumn<Guid>(
                name: "assigned_employee_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_pool_duty_schedules_status",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                sql: "status IN ('Scheduled', 'InProgress', 'Completed', 'Absent', 'Cancelled')");
        }
    }
}
