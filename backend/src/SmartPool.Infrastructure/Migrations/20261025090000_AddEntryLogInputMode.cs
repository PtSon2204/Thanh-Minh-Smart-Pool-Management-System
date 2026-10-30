using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Migrations;

// The baseline snapshot already contains InputMode, but no migration adds the column.
[DbContext(typeof(SmartPoolDbContext))]
[Migration("20261025090000_AddEntryLogInputMode")]
public sealed class AddEntryLogInputMode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "input_mode",
            schema: "smart_pool",
            table: "entry_logs",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "input_mode",
            schema: "smart_pool",
            table: "entry_logs");
    }
}
