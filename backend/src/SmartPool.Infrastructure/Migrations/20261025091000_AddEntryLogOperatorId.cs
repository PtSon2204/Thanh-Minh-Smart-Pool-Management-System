using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Migrations;

[DbContext(typeof(SmartPoolDbContext))]
[Migration("20261025091000_AddEntryLogOperatorId")]
public sealed class AddEntryLogOperatorId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "operator_id",
            schema: "smart_pool",
            table: "entry_logs",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_entry_logs_operator_id",
            schema: "smart_pool",
            table: "entry_logs",
            column: "operator_id");

        migrationBuilder.AddForeignKey(
            name: "entry_logs_operator_id_fkey",
            schema: "smart_pool",
            table: "entry_logs",
            column: "operator_id",
            principalSchema: "smart_pool",
            principalTable: "users",
            principalColumn: "id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "entry_logs_operator_id_fkey",
            schema: "smart_pool",
            table: "entry_logs");

        migrationBuilder.DropIndex(
            name: "IX_entry_logs_operator_id",
            schema: "smart_pool",
            table: "entry_logs");

        migrationBuilder.DropColumn(
            name: "operator_id",
            schema: "smart_pool",
            table: "entry_logs");
    }
}
