using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyInspections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS smart_pool.maintenance_logs CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS smart_pool.maintenance_schedules CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS smart_pool.daily_inspections CASCADE;");

            migrationBuilder.CreateTable(
                name: "daily_inspections",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    inspector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inspection_date = table.Column<DateOnly>(type: "date", nullable: false),
                    inspection_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    items_json = table.Column<string>(type: "jsonb", nullable: false),
                    overall_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValueSql: "'Passed'::character varying"),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("daily_inspections_pkey", x => x.id);
                    table.ForeignKey(
                        name: "daily_inspections_inspector_id_fkey",
                        column: x => x.inspector_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "daily_inspections_shift_id_fkey",
                        column: x => x.shift_id,
                        principalSchema: "smart_pool",
                        principalTable: "shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_inspections_inspector_id",
                schema: "smart_pool",
                table: "daily_inspections",
                column: "inspector_id");

            migrationBuilder.CreateIndex(
                name: "IX_daily_inspections_shift_id",
                schema: "smart_pool",
                table: "daily_inspections",
                column: "shift_id");

            migrationBuilder.CreateIndex(
                name: "uq_daily_inspections_date_shift",
                schema: "smart_pool",
                table: "daily_inspections",
                columns: new[] { "inspection_date", "shift_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_inspections",
                schema: "smart_pool");

            migrationBuilder.CreateTable(
                name: "maintenance_schedules",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    assigned_to = table.Column<Guid>(type: "uuid", nullable: true),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    description = table.Column<string>(type: "text", nullable: true),
                    planned_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValueSql: "'Pending'::character varying"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("maintenance_schedules_pkey", x => x.id);
                    table.ForeignKey(
                        name: "maintenance_schedules_assigned_to_fkey",
                        column: x => x.assigned_to,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "maintenance_schedules_equipment_id_fkey",
                        column: x => x.equipment_id,
                        principalSchema: "smart_pool",
                        principalTable: "equipments",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "maintenance_logs",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    performed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actual_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true, defaultValueSql: "0"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    note = table.Column<string>(type: "text", nullable: true),
                    vendor_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("maintenance_logs_pkey", x => x.id);
                    table.ForeignKey(
                        name: "maintenance_logs_equipment_id_fkey",
                        column: x => x.equipment_id,
                        principalSchema: "smart_pool",
                        principalTable: "equipments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "maintenance_logs_performed_by_fkey",
                        column: x => x.performed_by,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "maintenance_logs_schedule_id_fkey",
                        column: x => x.schedule_id,
                        principalSchema: "smart_pool",
                        principalTable: "maintenance_schedules",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_logs_equipment_id",
                schema: "smart_pool",
                table: "maintenance_logs",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_logs_performed_by",
                schema: "smart_pool",
                table: "maintenance_logs",
                column: "performed_by");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_logs_schedule_id",
                schema: "smart_pool",
                table: "maintenance_logs",
                column: "schedule_id");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_schedules_assigned_to",
                schema: "smart_pool",
                table: "maintenance_schedules",
                column: "assigned_to");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_schedules_equipment_id",
                schema: "smart_pool",
                table: "maintenance_schedules",
                column: "equipment_id");
        }
    }
}
