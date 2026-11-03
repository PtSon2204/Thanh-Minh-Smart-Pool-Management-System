using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTotalEntriesToTicketType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "remaining_entries",
                schema: "smart_pool",
                table: "tickets",
                type: "integer",
                nullable: true,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "total_entries",
                schema: "smart_pool",
                table: "ticket_types",
                type: "integer",
                nullable: true,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "remaining_entries",
                schema: "smart_pool",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "total_entries",
                schema: "smart_pool",
                table: "ticket_types");
        }
    }
}
