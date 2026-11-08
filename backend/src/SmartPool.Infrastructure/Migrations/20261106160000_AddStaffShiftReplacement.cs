using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartPool.Infrastructure.Persistence.DbContext;

namespace SmartPool.Infrastructure.Migrations;

[DbContext(typeof(SmartPoolDbContext))]
[Migration("20261106160000_AddStaffShiftReplacement")]
public sealed class AddStaffShiftReplacement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "DestinationEmployeeId",
            schema: "smart_pool",
            table: "staff_schedule_exceptions",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_staff_schedule_exceptions_DestinationEmployeeId",
            schema: "smart_pool",
            table: "staff_schedule_exceptions",
            column: "DestinationEmployeeId");

        migrationBuilder.AddForeignKey(
            name: "FK_staff_schedule_exceptions_employees_DestinationEmployeeId",
            schema: "smart_pool",
            table: "staff_schedule_exceptions",
            column: "DestinationEmployeeId",
            principalSchema: "smart_pool",
            principalTable: "employees",
            principalColumn: "user_id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropCheckConstraint(
            name: "ck_schedule_exception_kind",
            schema: "smart_pool",
            table: "staff_schedule_exceptions");

        migrationBuilder.AddCheckConstraint(
            name: "ck_schedule_exception_kind",
            schema: "smart_pool",
            table: "staff_schedule_exceptions",
            sql: "\"Kind\" IN ('Leave','Move','Replace')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM smart_pool.staff_schedule_exceptions WHERE "Kind" = 'Replace') THEN
                    RAISE EXCEPTION 'Cannot roll back staff shift replacement while replacement history exists.';
                END IF;
            END $$;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_staff_schedule_exceptions_employees_DestinationEmployeeId",
            schema: "smart_pool",
            table: "staff_schedule_exceptions");

        migrationBuilder.DropIndex(
            name: "IX_staff_schedule_exceptions_DestinationEmployeeId",
            schema: "smart_pool",
            table: "staff_schedule_exceptions");

        migrationBuilder.DropCheckConstraint(
            name: "ck_schedule_exception_kind",
            schema: "smart_pool",
            table: "staff_schedule_exceptions");

        migrationBuilder.AddCheckConstraint(
            name: "ck_schedule_exception_kind",
            schema: "smart_pool",
            table: "staff_schedule_exceptions",
            sql: "\"Kind\" IN ('Leave','Move')");

        migrationBuilder.DropColumn(
            name: "DestinationEmployeeId",
            schema: "smart_pool",
            table: "staff_schedule_exceptions");
    }
}
