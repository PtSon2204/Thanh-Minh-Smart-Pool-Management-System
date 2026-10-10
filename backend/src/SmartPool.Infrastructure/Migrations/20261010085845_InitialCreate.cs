using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_shifts",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "entry_logs",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "incidents",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "inventory_logs",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "maintenance_logs",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "order_details",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "rentals",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "salaries",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "user_profiles",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "shifts",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "tickets",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "maintenance_schedules",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "products",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "ticket_types",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "equipments",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "employees",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "vouchers",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "users",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "smart_pool");
        }
    }
}
