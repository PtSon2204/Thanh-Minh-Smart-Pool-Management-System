using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalTaskExecutionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "due_notice_sent_at_utc",
                schema: "smart_pool",
                table: "pool_duty_schedules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pool_duty_task_executions",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    operational_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_admin_override = table.Column<bool>(type: "boolean", nullable: false),
                    task_revision = table.Column<long>(type: "bigint", nullable: false),
                    executed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    water_quality_log_id = table.Column<Guid>(type: "uuid", nullable: true),
                    daily_inspection_id = table.Column<Guid>(type: "uuid", nullable: true),
                    result_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pool_duty_task_executions",
                schema: "smart_pool");

            migrationBuilder.DropColumn(
                name: "due_notice_sent_at_utc",
                schema: "smart_pool",
                table: "pool_duty_schedules");
        }
    }
}
