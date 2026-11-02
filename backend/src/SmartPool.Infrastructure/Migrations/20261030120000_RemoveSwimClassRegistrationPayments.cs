using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations;

public partial class RemoveSwimClassRegistrationPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A registration awaiting in-system payment must not occupy a place after
        // the payment workflow is removed. It can be registered again directly.
        migrationBuilder.Sql("""
            UPDATE smart_pool.class_registrations
            SET status = 'Cancelled', cancelled_at = GREATEST(now(), registered_at),
                confirmed_at = NULL, updated_at = GREATEST(now(), registered_at)
            WHERE status NOT IN ('Confirmed', 'Cancelled');
            """);

        migrationBuilder.DropForeignKey(
            name: "class_registrations_order_id_fkey", schema: "smart_pool", table: "class_registrations");
        migrationBuilder.DropIndex(
            name: "ix_class_registrations_order_id", schema: "smart_pool", table: "class_registrations");
        migrationBuilder.DropCheckConstraint(
            name: "ck_class_registrations_amount_nonnegative", schema: "smart_pool", table: "class_registrations");
        migrationBuilder.DropColumn(name: "order_id", schema: "smart_pool", table: "class_registrations");
        migrationBuilder.DropColumn(name: "amount", schema: "smart_pool", table: "class_registrations");
        migrationBuilder.AddCheckConstraint(
            name: "ck_class_registrations_status", schema: "smart_pool", table: "class_registrations",
            sql: "status IN ('Confirmed', 'Cancelled')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_class_registrations_status", schema: "smart_pool", table: "class_registrations");
        migrationBuilder.AddColumn<Guid>(
            name: "order_id", schema: "smart_pool", table: "class_registrations",
            type: "uuid", nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "amount", schema: "smart_pool", table: "class_registrations",
            type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
        migrationBuilder.CreateIndex(
            name: "ix_class_registrations_order_id", schema: "smart_pool", table: "class_registrations",
            column: "order_id");
        migrationBuilder.AddForeignKey(
            name: "class_registrations_order_id_fkey", schema: "smart_pool", table: "class_registrations",
            column: "order_id", principalSchema: "smart_pool", principalTable: "orders",
            principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddCheckConstraint(
            name: "ck_class_registrations_amount_nonnegative", schema: "smart_pool", table: "class_registrations",
            sql: "amount >= 0");
    }
}
