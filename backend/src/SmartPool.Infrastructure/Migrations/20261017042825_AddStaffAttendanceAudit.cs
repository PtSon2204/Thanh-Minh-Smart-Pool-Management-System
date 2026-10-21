using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAttendanceAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "check_in_method",
                schema: "smart_pool",
                table: "employee_shifts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "check_out_method",
                schema: "smart_pool",
                table: "employee_shifts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "attendance_logs",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    employee_shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    performed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    before_check_in_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    before_check_out_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    before_check_in_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    before_check_out_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    before_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    after_check_in_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    after_check_out_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    after_check_in_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    after_check_out_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    after_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("attendance_logs_pkey", x => x.id);
                    table.ForeignKey(
                        name: "attendance_logs_performed_by_user_id_fkey",
                        column: x => x.performed_by_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "attendance_logs_employee_shift_id_fkey",
                        column: x => x.employee_shift_id,
                        principalSchema: "smart_pool",
                        principalTable: "employee_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_logs_performed_by_user_id",
                schema: "smart_pool",
                table: "attendance_logs",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_logs_employee_shift_id",
                schema: "smart_pool",
                table: "attendance_logs",
                column: "employee_shift_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_logs_performed_at_utc",
                schema: "smart_pool",
                table: "attendance_logs",
                column: "performed_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_logs",
                schema: "smart_pool");

            migrationBuilder.DropColumn(
                name: "check_in_method",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.DropColumn(
                name: "check_out_method",
                schema: "smart_pool",
                table: "employee_shifts");
        }
    }
}
