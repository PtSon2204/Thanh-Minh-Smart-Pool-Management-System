using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaterQualityLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "water_quality_logs",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    inspection_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ph_value = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    free_chlorine_mg_l = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    is_water_clear = table.Column<bool>(type: "boolean", nullable: true),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Manual"),
                    inspected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_event_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "NotEvaluated"),
                    recommendation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    evaluation_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("water_quality_logs_pkey", x => x.id);
                    table.CheckConstraint("ck_water_quality_logs_chlorine_nonnegative", "free_chlorine_mg_l >= 0");
                    table.CheckConstraint("ck_water_quality_logs_ph_nonnegative", "ph_value >= 0");
                    table.CheckConstraint("ck_water_quality_logs_source", "source IN ('Manual', 'Device')");
                    table.CheckConstraint("ck_water_quality_logs_source_identity", "(source = 'Manual' AND inspected_by IS NOT NULL AND source_event_id IS NULL)\nOR\n(source = 'Device' AND inspected_by IS NULL AND equipment_id IS NOT NULL\n    AND source_event_id IS NOT NULL AND length(btrim(source_event_id)) > 0)");
                    table.CheckConstraint("ck_water_quality_logs_status", "status IN ('NotEvaluated', 'Incomplete', 'WithinLimits', 'Warning', 'Danger')");
                    table.ForeignKey(
                        name: "water_quality_logs_equipment_id_fkey",
                        column: x => x.equipment_id,
                        principalSchema: "smart_pool",
                        principalTable: "equipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "water_quality_logs_inspected_by_fkey",
                        column: x => x.inspected_by,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_water_quality_logs_inspected_by",
                schema: "smart_pool",
                table: "water_quality_logs",
                column: "inspected_by");

            migrationBuilder.CreateIndex(
                name: "ix_water_quality_logs_inspection_time",
                schema: "smart_pool",
                table: "water_quality_logs",
                column: "inspection_time",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ux_water_quality_logs_device_event",
                schema: "smart_pool",
                table: "water_quality_logs",
                columns: new[] { "equipment_id", "source_event_id" },
                unique: true,
                filter: "source = 'Device'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "water_quality_logs",
                schema: "smart_pool");
        }
    }
}
