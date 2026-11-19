using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Migrations;

[DbContext(typeof(SmartPoolDbContext))]
[Migration("20261119040000_ManualStaffReactivation")]
public sealed class ManualStaffReactivation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<bool>(
            name: "manual_access_enabled",
            schema: "smart_pool",
            table: "employees",
            type: "boolean",
            nullable: false,
            defaultValue: false);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "manual_access_enabled",
            schema: "smart_pool",
            table: "employees");
}
