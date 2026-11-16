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
        migrationBuilder.Sql("""
            ALTER TABLE smart_pool.entry_logs
            ADD COLUMN IF NOT EXISTS operator_id uuid;

            CREATE INDEX IF NOT EXISTS "IX_entry_logs_operator_id"
            ON smart_pool.entry_logs (operator_id);

            DO $EF$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'entry_logs_operator_id_fkey'
                        AND conrelid = 'smart_pool.entry_logs'::regclass
                ) THEN
                    ALTER TABLE smart_pool.entry_logs
                    ADD CONSTRAINT entry_logs_operator_id_fkey
                    FOREIGN KEY (operator_id) REFERENCES smart_pool.users (id);
                END IF;
            END $EF$;
            """);
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
