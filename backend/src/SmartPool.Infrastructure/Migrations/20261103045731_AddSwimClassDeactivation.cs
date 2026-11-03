using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSwimClassDeactivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "inactivated_at",
                schema: "smart_pool",
                table: "swim_classes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inactive_reason",
                schema: "smart_pool",
                table: "swim_classes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "inactivated_at",
                schema: "smart_pool",
                table: "swim_classes");

            migrationBuilder.DropColumn(
                name: "inactive_reason",
                schema: "smart_pool",
                table: "swim_classes");
        }
    }
}
