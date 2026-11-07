using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffScheduleDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_schedule_deliveries",
                schema: "smart_pool",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_schedule_deliveries", x => x.Id);
                    table.CheckConstraint("ck_staff_schedule_delivery_status", "\"Status\" IN ('Pending','Sending','Sent','Failed','Unknown')");
                    table.ForeignKey(
                        name: "FK_staff_schedule_deliveries_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "smart_pool",
                        principalTable: "employees",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_schedule_deliveries_users_RecipientId",
                        column: x => x.RecipientId,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_deliveries_EmployeeId",
                schema: "smart_pool",
                table: "staff_schedule_deliveries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_deliveries_OperationId_RecipientId_Channel",
                schema: "smart_pool",
                table: "staff_schedule_deliveries",
                columns: new[] { "OperationId", "RecipientId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_deliveries_RecipientId",
                schema: "smart_pool",
                table: "staff_schedule_deliveries",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_schedule_deliveries_Status_NextAttemptAtUtc_CreatedAt~",
                schema: "smart_pool",
                table: "staff_schedule_deliveries",
                columns: new[] { "Status", "NextAttemptAtUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_schedule_deliveries",
                schema: "smart_pool");
        }
    }
}
