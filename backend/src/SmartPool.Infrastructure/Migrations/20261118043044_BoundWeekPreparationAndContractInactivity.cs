using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartPool.Infrastructure.Persistence.DbContext;

#nullable disable

namespace SmartPool.Infrastructure.Migrations;

[DbContext(typeof(SmartPoolDbContext))]
[Migration("20261118043044_BoundWeekPreparationAndContractInactivity")]
public partial class BoundWeekPreparationAndContractInactivity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "week_start",
            schema: "smart_pool",
            table: "pool_duty_measurement_recurrences",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "contract_inactive_since_utc",
            schema: "smart_pool",
            table: "employees",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.DropIndex(
            name: "ux_pool_duty_measurement_recurrences_active_pattern",
            schema: "smart_pool",
            table: "pool_duty_measurement_recurrences");

        migrationBuilder.Sql("UPDATE smart_pool.pool_duty_measurement_recurrences SET is_active = false WHERE week_start IS NULL;");

        migrationBuilder.CreateIndex(
            name: "ux_pool_duty_measurement_recurrences_week_start",
            schema: "smart_pool",
            table: "pool_duty_measurement_recurrences",
            column: "week_start",
            unique: true,
            filter: "week_start IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE smart_pool.pool_duty_measurement_recurrences SET is_active = false WHERE week_start IS NOT NULL;");

        migrationBuilder.DropIndex(
            name: "ux_pool_duty_measurement_recurrences_week_start",
            schema: "smart_pool",
            table: "pool_duty_measurement_recurrences");

        migrationBuilder.DropColumn(
            name: "week_start",
            schema: "smart_pool",
            table: "pool_duty_measurement_recurrences");

        migrationBuilder.DropColumn(
            name: "contract_inactive_since_utc",
            schema: "smart_pool",
            table: "employees");

        migrationBuilder.CreateIndex(
            name: "ux_pool_duty_measurement_recurrences_active_pattern",
            schema: "smart_pool",
            table: "pool_duty_measurement_recurrences",
            columns: new[] { "anchor_date", "scheduled_time", "shift_id", "preferred_employee_id" },
            unique: true,
            filter: "is_active = true");
    }
}
