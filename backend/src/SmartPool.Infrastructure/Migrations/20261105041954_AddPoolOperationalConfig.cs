using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPoolOperationalConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PoolOperationalConfigs",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, defaultValue: "Bể bơi Thành Minh"),
                    MaxCapacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    WarningThresholdPercent = table.Column<int>(type: "integer", nullable: false, defaultValue: 80),
                    MorningStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    MorningEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    AfternoonStartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    AfternoonEndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    OperationalStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Operating"),
                    StatusReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsAutoSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolOperationalConfigs", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "smart_pool",
                table: "PoolOperationalConfigs",
                columns: new[] { "Id", "AfternoonEndTime", "AfternoonStartTime", "IsAutoSuspended", "MaxCapacity", "MorningEndTime", "MorningStartTime", "OperationalStatus", "PoolName", "StatusReason", "UpdatedAt", "UpdatedBy", "WarningThresholdPercent" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new TimeOnly(19, 30, 0), new TimeOnly(13, 30, 0), false, 100, new TimeOnly(11, 30, 0), new TimeOnly(6, 0, 0), "Operating", "Bể bơi Thành Minh", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", 80 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PoolOperationalConfigs",
                schema: "smart_pool");
        }
    }
}
