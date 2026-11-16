using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPreferredEmployeeToPoolDutySchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "pool_duty_measurement_recurrences_employee_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences");

            migrationBuilder.DropIndex(
                name: "ix_pool_duty_measurement_recurrences_active_anchor",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences");

            migrationBuilder.AlterColumn<Guid>(
                name: "EmployeeId",
                schema: "smart_pool",
                table: "staff_schedule_deliveries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "preferred_employee_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pool_duty_schedules_preferred_employee_id",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "preferred_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_pool_duty_measurement_recurrences_active_next_occurrence",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences",
                columns: new[] { "is_active", "next_occurrence_date" });

            migrationBuilder.CreateIndex(
                name: "ux_pool_duty_measurement_recurrences_active_pattern",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences",
                columns: new[] { "anchor_date", "scheduled_time", "shift_id", "preferred_employee_id" },
                unique: true,
                filter: "is_active = true");

            migrationBuilder.AddForeignKey(
                name: "pool_duty_measurement_recurrences_employee_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences",
                column: "preferred_employee_id",
                principalSchema: "smart_pool",
                principalTable: "employees",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "pool_duty_schedules_preferred_employee_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                column: "preferred_employee_id",
                principalSchema: "smart_pool",
                principalTable: "employees",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "pool_duty_measurement_recurrences_employee_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences");

            migrationBuilder.DropForeignKey(
                name: "pool_duty_schedules_preferred_employee_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropIndex(
                name: "IX_pool_duty_schedules_preferred_employee_id",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.DropIndex(
                name: "ix_pool_duty_measurement_recurrences_active_next_occurrence",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences");

            migrationBuilder.DropIndex(
                name: "ux_pool_duty_measurement_recurrences_active_pattern",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences");

            migrationBuilder.DropColumn(
                name: "preferred_employee_id",
                schema: "smart_pool",
                table: "pool_duty_schedules");

            migrationBuilder.AlterColumn<Guid>(
                name: "EmployeeId",
                schema: "smart_pool",
                table: "staff_schedule_deliveries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pool_duty_measurement_recurrences_active_anchor",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences",
                columns: new[] { "is_active", "anchor_date" });

            migrationBuilder.AddForeignKey(
                name: "pool_duty_measurement_recurrences_employee_id_fkey",
                schema: "smart_pool",
                table: "pool_duty_measurement_recurrences",
                column: "preferred_employee_id",
                principalSchema: "smart_pool",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
