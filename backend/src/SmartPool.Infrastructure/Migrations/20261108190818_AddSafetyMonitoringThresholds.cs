using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSafetyMonitoringThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "safety_monitoring_thresholds",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    confidence_threshold = table.Column<double>(type: "double precision", nullable: false),
                    warning_ratio = table.Column<double>(type: "double precision", nullable: false),
                    critical_ratio = table.Column<double>(type: "double precision", nullable: false),
                    recovery_ratio = table.Column<double>(type: "double precision", nullable: false),
                    critical_hold_seconds = table.Column<double>(type: "double precision", nullable: false),
                    recovery_hold_seconds = table.Column<double>(type: "double precision", nullable: false),
                    cooldown_seconds = table.Column<double>(type: "double precision", nullable: false),
                    window_seconds = table.Column<double>(type: "double precision", nullable: false),
                    max_observation_gap_seconds = table.Column<double>(type: "double precision", nullable: false),
                    min_observations = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_safety_monitoring_thresholds", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "smart_pool",
                table: "safety_monitoring_thresholds",
                columns: new[] { "id", "confidence_threshold", "cooldown_seconds", "created_at", "critical_hold_seconds", "critical_ratio", "max_observation_gap_seconds", "min_observations", "recovery_hold_seconds", "recovery_ratio", "updated_at", "updated_by", "warning_ratio", "window_seconds" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000042"), 0.59999999999999998, 15.0, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3.0, 0.69999999999999996, 3.0, 3, 5.0, 0.20000000000000001, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0.40000000000000002, 5.0 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "safety_monitoring_thresholds",
                schema: "smart_pool");
        }
    }
}
