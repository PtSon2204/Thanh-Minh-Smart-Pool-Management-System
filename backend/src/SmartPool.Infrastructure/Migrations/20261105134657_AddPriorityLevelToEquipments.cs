using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriorityLevelToEquipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "priority_level",
                schema: "smart_pool",
                table: "equipments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                defaultValueSql: "'Normal'::character varying");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "priority_level",
                schema: "smart_pool",
                table: "equipments");
        }
    }
}
