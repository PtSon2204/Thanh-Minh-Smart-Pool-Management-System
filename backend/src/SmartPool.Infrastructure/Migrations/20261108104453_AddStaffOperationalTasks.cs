using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffOperationalTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pool_duty_schedules_assigned_employee_id",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.AddColumn<Guid>(
                name: "operational_task_id",
                schema: "smart_pool",
                table: "water_quality_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "shift_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "due_at_utc",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "employee_shift_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inspection_window",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "measurement_recurrence_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "revision",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<DateTime>(
                name: "scheduled_at_utc",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "task_type",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "trigger_water_quality_log_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unassigned_reason",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "operational_task_id",
                schema: "smart_pool",
                table: "daily_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pool_duty_measurement_recurrences",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    anchor_date = table.Column<DateOnly>(type: "date", nullable: false),
                    next_occurrence_date = table.Column<DateOnly>(type: "date", nullable: false),
                    scheduled_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preferred_employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pool_duty_measurement_recurrences_pkey", x => x.id);
                    table.ForeignKey(
                        name: "pool_duty_measurement_recurrences_created_by_fkey",
                        column: x => x.created_by,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "pool_duty_measurement_recurrences_employee_id_fkey",
                        column: x => x.preferred_employee_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "pool_duty_measurement_recurrences_shift_id_fkey",
                        column: x => x.shift_id,
                        principalSchema: "smart_pool",
                        principalTable: "shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_water_quality_logs_operational_task_id",
                schema: "smart_pool",
                table: "water_quality_logs",
                column: "operational_task_id");

            migrationBuilder.CreateIndex(
                name: "ix_pool_duty_schedules_employee_date",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                columns: new[] { "assigned_employee_id", "duty_date" });

            migrationBuilder.CreateIndex(
                name: "IX_pool_duty_schedules_employee_shift_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "employee_shift_id");

            migrationBuilder.CreateIndex(
                name: "ix_pool_duty_schedules_status_date",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                columns: new[] { "status", "duty_date" });

            migrationBuilder.CreateIndex(
                name: "ux_pool_duty_schedules_checklist_window",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                columns: new[] { "duty_date", "inspection_window" },
                unique: true,
                filter: "task_type = 'EquipmentChecklist' AND inspection_window IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_pool_duty_schedules_cleaning_trigger",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "trigger_water_quality_log_id",
                unique: true,
                filter: "task_type = 'Cleaning' AND trigger_water_quality_log_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_pool_duty_schedules_measurement_occurrence",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                columns: new[] { "measurement_recurrence_id", "duty_date" },
                unique: true,
                filter: "task_type = 'Measurement' AND measurement_recurrence_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pool_duty_schedules_assigned_shift",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                sql: "task_type IS NULL OR assigned_employee_id IS NULL OR employee_shift_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pool_duty_schedules_inspection_window",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                sql: "inspection_window IS NULL OR inspection_window IN ('MORNING', 'AFTERNOON')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pool_duty_schedules_task_links",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                sql: "(task_type IS NULL) OR (task_type = 'Measurement' AND inspection_window IS NULL AND trigger_water_quality_log_id IS NULL) OR (task_type = 'EquipmentChecklist' AND inspection_window IS NOT NULL AND measurement_recurrence_id IS NULL AND trigger_water_quality_log_id IS NULL) OR (task_type = 'Cleaning' AND trigger_water_quality_log_id IS NOT NULL AND measurement_recurrence_id IS NULL AND inspection_window IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pool_duty_schedules_task_type",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                sql: "task_type IS NULL OR task_type IN ('Measurement', 'EquipmentChecklist', 'Cleaning')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pool_duty_schedules_unassigned_cleaning",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                sql: "task_type <> 'Cleaning' OR assigned_employee_id IS NULL OR (employee_shift_id IS NOT NULL AND due_at_utc IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "uq_daily_inspections_operational_task_id",
                schema: "smart_pool",
                table: "daily_inspections",
                column: "operational_task_id",
                unique: true,
                filter: "operational_task_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_pool_duty_measurement_recurrences_active_anchor",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences",
                columns: new[] { "is_active", "anchor_date" });

            migrationBuilder.CreateIndex(
                name: "IX_pool_duty_measurement_recurrences_created_by",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_pool_duty_measurement_recurrences_preferred_employee_id",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences",
                column: "preferred_employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_pool_duty_measurement_recurrences_shift_id",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences",
                column: "shift_id");

            migrationBuilder.AddForeignKey(
                name: "daily_inspections_operational_task_id_fkey",
                schema: "smart_pool",
                table: "daily_inspections",
                column: "operational_task_id",
                principalSchema: "smart_pool",
                principalTable: "pool_duty_schedules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "pool_duty_schedules_employee_shift_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "employee_shift_id",
                principalSchema: "smart_pool",
                principalTable: "employee_shifts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "pool_duty_schedules_measurement_recurrence_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "measurement_recurrence_id",
                principalSchema: "smart_pool",
                principalTable: "pool_duty_measurement_recurrences",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "pool_duty_schedules_trigger_water_quality_log_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "trigger_water_quality_log_id",
                principalSchema: "smart_pool",
                principalTable: "water_quality_logs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "water_quality_logs_operational_task_id_fkey",
                schema: "smart_pool",
                table: "water_quality_logs",
                column: "operational_task_id",
                principalSchema: "smart_pool",
                principalTable: "pool_duty_schedules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "daily_inspections_operational_task_id_fkey",
                schema: "smart_pool",
                table: "daily_inspections");

            migrationBuilder.DropForeignKey(
                name: "pool_duty_schedules_employee_shift_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropForeignKey(
                name: "pool_duty_schedules_measurement_recurrence_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropForeignKey(
                name: "pool_duty_schedules_trigger_water_quality_log_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropForeignKey(
                name: "water_quality_logs_operational_task_id_fkey",
                schema: "smart_pool",
                table: "water_quality_logs");

            migrationBuilder.DropTable(
                name: "pool_duty_measurement_recurrences",
                schema: "smart_pool");

            migrationBuilder.DropIndex(
                name: "ix_water_quality_logs_operational_task_id",
                schema: "smart_pool",
                table: "water_quality_logs");

            migrationBuilder.DropIndex(
                name: "ix_pool_duty_schedules_employee_date",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropIndex(
                name: "IX_pool_duty_schedules_employee_shift_id",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropIndex(
                name: "ix_pool_duty_schedules_status_date",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropIndex(
                name: "ux_pool_duty_schedules_checklist_window",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropIndex(
                name: "ux_pool_duty_schedules_cleaning_trigger",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropIndex(
                name: "ux_pool_duty_schedules_measurement_occurrence",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pool_duty_schedules_assigned_shift",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pool_duty_schedules_inspection_window",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pool_duty_schedules_task_links",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pool_duty_schedules_task_type",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pool_duty_schedules_unassigned_cleaning",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropIndex(
                name: "uq_daily_inspections_operational_task_id",
                schema: "smart_pool",
                table: "daily_inspections");

            migrationBuilder.DropColumn(
                name: "operational_task_id",
                schema: "smart_pool",
                table: "water_quality_logs");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "due_at_utc",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "employee_shift_id",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "inspection_window",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "measurement_recurrence_id",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "revision",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "scheduled_at_utc",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "task_type",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "trigger_water_quality_log_id",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "unassigned_reason",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropColumn(
                name: "operational_task_id",
                schema: "smart_pool",
                table: "daily_inspections");

            migrationBuilder.AlterColumn<Guid>(
                name: "shift_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pool_duty_schedules_assigned_employee_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "assigned_employee_id");
        }
    }
}
