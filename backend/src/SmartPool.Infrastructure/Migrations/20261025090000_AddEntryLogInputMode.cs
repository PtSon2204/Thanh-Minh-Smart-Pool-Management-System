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
        migrationBuilder.Sql("""
            ALTER TABLE smart_pool.entry_logs
            ADD COLUMN IF NOT EXISTS input_mode character varying(50);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "input_mode",
            schema: "smart_pool",
            table: "entry_logs");
    }
}
