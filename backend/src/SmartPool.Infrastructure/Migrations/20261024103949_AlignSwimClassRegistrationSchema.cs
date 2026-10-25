using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations;

/// <summary>
/// Aligns class registrations with whole classes. Existing class data requires a
/// separately reviewed backfill because fees, registration windows and duplicate
/// per-session registrations cannot be inferred from the old schema.
/// </summary>
public partial class AlignSwimClassRegistrationSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This data-dependent guard must run before any destructive schema operation.
        migrationBuilder.Sql("""
            DO $migration$
            BEGIN
                IF EXISTS (SELECT 1 FROM smart_pool.swim_classes) THEN
                    RAISE EXCEPTION 'Cannot align populated swim class tables automatically'
                        USING HINT = 'Backfill fee and registration window, reconcile per-session registrations and attendance IDs, then use a reviewed data migration.';
                END IF;
            END
            $migration$;
            """);

        migrationBuilder.DropForeignKey(
            name: "class_attendances_registration_session_fkey",
            schema: "smart_pool",
            table: "class_attendances");
        migrationBuilder.DropForeignKey(
            name: "class_registrations_reviewed_by_user_id_fkey",
            schema: "smart_pool",
            table: "class_registrations");
        migrationBuilder.DropForeignKey(
            name: "class_registrations_session_id_fkey",
            schema: "smart_pool",
            table: "class_registrations");
        migrationBuilder.DropUniqueConstraint(
            name: "ak_class_registrations_id_session_id",
            schema: "smart_pool",
            table: "class_registrations");
        migrationBuilder.DropCheckConstraint(
            name: "ck_class_registrations_review_fields_pair",
            schema: "smart_pool",
            table: "class_registrations");

        foreach (var name in new[]
        {
            "ix_class_attendances_session_id",
            "ux_class_attendances_registration_session"
        })
        {
            migrationBuilder.DropIndex(name: name, schema: "smart_pool", table: "class_attendances");
        }

        foreach (var name in new[]
        {
            "ux_class_registrations_session_customer",
            "ix_class_registrations_status_requested_at",
            "ix_class_registrations_reviewed_by_user_id"
        })
        {
            migrationBuilder.DropIndex(name: name, schema: "smart_pool", table: "class_registrations");
        }

        migrationBuilder.AddColumn<decimal>(
            name: "fee", schema: "smart_pool", table: "swim_classes",
            type: "numeric(18,2)", precision: 18, scale: 2, nullable: false);
        migrationBuilder.AddColumn<DateTime>(
            name: "registration_start_at", schema: "smart_pool", table: "swim_classes",
            type: "timestamp with time zone", nullable: false);
        migrationBuilder.AddColumn<DateTime>(
            name: "registration_end_at", schema: "smart_pool", table: "swim_classes",
            type: "timestamp with time zone", nullable: false);
        migrationBuilder.AddCheckConstraint(
            name: "ck_swim_classes_fee_nonnegative", schema: "smart_pool",
            table: "swim_classes", sql: "fee >= 0");
        migrationBuilder.AddCheckConstraint(
            name: "ck_swim_classes_registration_time_order", schema: "smart_pool",
            table: "swim_classes", sql: "registration_start_at < registration_end_at");

        foreach (var name in new[]
        {
            "session_id", "requested_at", "reviewed_by_user_id", "reviewed_at"
        })
        {
            migrationBuilder.DropColumn(name: name, schema: "smart_pool", table: "class_registrations");
        }

        migrationBuilder.AddColumn<Guid>(
            name: "class_id", schema: "smart_pool", table: "class_registrations",
            type: "uuid", nullable: false);
        migrationBuilder.AddColumn<Guid>(
            name: "order_id", schema: "smart_pool", table: "class_registrations",
            type: "uuid", nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "amount", schema: "smart_pool", table: "class_registrations",
            type: "numeric(18,2)", precision: 18, scale: 2, nullable: false);
        migrationBuilder.AddColumn<DateTime>(
            name: "registered_at", schema: "smart_pool", table: "class_registrations",
            type: "timestamp with time zone", nullable: false, defaultValueSql: "now()");
        migrationBuilder.AddColumn<DateTime>(
            name: "confirmed_at", schema: "smart_pool", table: "class_registrations",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "cancelled_at", schema: "smart_pool", table: "class_registrations",
            type: "timestamp with time zone", nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_class_registrations_amount_nonnegative", schema: "smart_pool",
            table: "class_registrations", sql: "amount >= 0");
        migrationBuilder.AddCheckConstraint(
            name: "ck_class_registrations_confirmed_after_registered", schema: "smart_pool",
            table: "class_registrations", sql: "confirmed_at IS NULL OR confirmed_at >= registered_at");
        migrationBuilder.AddCheckConstraint(
            name: "ck_class_registrations_cancelled_after_registered", schema: "smart_pool",
            table: "class_registrations", sql: "cancelled_at IS NULL OR cancelled_at >= registered_at");
        migrationBuilder.AddForeignKey(
            name: "class_registrations_class_id_fkey", schema: "smart_pool",
            table: "class_registrations", column: "class_id",
            principalSchema: "smart_pool", principalTable: "swim_classes",
            principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "class_registrations_order_id_fkey", schema: "smart_pool",
            table: "class_registrations", column: "order_id",
            principalSchema: "smart_pool", principalTable: "orders",
            principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.CreateIndex(
            name: "ux_class_registrations_class_customer", schema: "smart_pool",
            table: "class_registrations", columns: new[] { "class_id", "customer_user_id" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_class_registrations_status_registered_at", schema: "smart_pool",
            table: "class_registrations", columns: new[] { "status", "registered_at" });
        migrationBuilder.CreateIndex(
            name: "ix_class_registrations_order_id", schema: "smart_pool",
            table: "class_registrations", column: "order_id");

        migrationBuilder.AddForeignKey(
            name: "class_attendances_registration_id_fkey", schema: "smart_pool",
            table: "class_attendances", column: "registration_id",
            principalSchema: "smart_pool", principalTable: "class_registrations",
            principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.CreateIndex(
            name: "ix_class_attendances_registration_id", schema: "smart_pool",
            table: "class_attendances", column: "registration_id");
        migrationBuilder.CreateIndex(
            name: "ux_class_attendances_session_registration", schema: "smart_pool",
            table: "class_attendances", columns: new[] { "session_id", "registration_id" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reconstructing an old session ID from a class-level registration is unsafe.
        migrationBuilder.Sql("""
            DO $migration$
            BEGIN
                IF EXISTS (SELECT 1 FROM smart_pool.swim_classes) THEN
                    RAISE EXCEPTION 'Cannot downgrade populated swim class tables automatically'
                        USING HINT = 'The previous schema requires one session per registration; prepare a reviewed reverse data migration first.';
                END IF;
            END
            $migration$;
            """);

        migrationBuilder.DropForeignKey(
            name: "class_attendances_registration_id_fkey",
            schema: "smart_pool", table: "class_attendances");
        foreach (var name in new[]
        {
            "ix_class_attendances_registration_id",
            "ux_class_attendances_session_registration"
        })
        {
            migrationBuilder.DropIndex(name: name, schema: "smart_pool", table: "class_attendances");
        }

        foreach (var name in new[]
        {
            "ux_class_registrations_class_customer",
            "ix_class_registrations_status_registered_at",
            "ix_class_registrations_order_id"
        })
        {
            migrationBuilder.DropIndex(name: name, schema: "smart_pool", table: "class_registrations");
        }
        migrationBuilder.DropForeignKey(
            name: "class_registrations_class_id_fkey",
            schema: "smart_pool", table: "class_registrations");
        migrationBuilder.DropForeignKey(
            name: "class_registrations_order_id_fkey",
            schema: "smart_pool", table: "class_registrations");
        foreach (var name in new[]
        {
            "ck_class_registrations_amount_nonnegative",
            "ck_class_registrations_confirmed_after_registered",
            "ck_class_registrations_cancelled_after_registered"
        })
        {
            migrationBuilder.DropCheckConstraint(name: name, schema: "smart_pool", table: "class_registrations");
        }
        foreach (var name in new[]
        {
            "class_id", "order_id", "amount", "registered_at", "confirmed_at", "cancelled_at"
        })
        {
            migrationBuilder.DropColumn(name: name, schema: "smart_pool", table: "class_registrations");
        }

        migrationBuilder.AddColumn<Guid>(
            name: "session_id", schema: "smart_pool", table: "class_registrations",
            type: "uuid", nullable: false);
        migrationBuilder.AddColumn<DateTime>(
            name: "requested_at", schema: "smart_pool", table: "class_registrations",
            type: "timestamp with time zone", nullable: false, defaultValueSql: "now()");
        migrationBuilder.AddColumn<Guid>(
            name: "reviewed_by_user_id", schema: "smart_pool", table: "class_registrations",
            type: "uuid", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "reviewed_at", schema: "smart_pool", table: "class_registrations",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddUniqueConstraint(
            name: "ak_class_registrations_id_session_id", schema: "smart_pool",
            table: "class_registrations", columns: new[] { "id", "session_id" });
        migrationBuilder.AddCheckConstraint(
            name: "ck_class_registrations_review_fields_pair", schema: "smart_pool",
            table: "class_registrations", sql: "(reviewed_by_user_id IS NULL) = (reviewed_at IS NULL)");
        migrationBuilder.AddForeignKey(
            name: "class_registrations_session_id_fkey", schema: "smart_pool",
            table: "class_registrations", column: "session_id",
            principalSchema: "smart_pool", principalTable: "class_sessions",
            principalColumn: "id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "class_registrations_reviewed_by_user_id_fkey", schema: "smart_pool",
            table: "class_registrations", column: "reviewed_by_user_id",
            principalSchema: "smart_pool", principalTable: "users",
            principalColumn: "id", onDelete: ReferentialAction.SetNull);
        migrationBuilder.CreateIndex(
            name: "ux_class_registrations_session_customer", schema: "smart_pool",
            table: "class_registrations", columns: new[] { "session_id", "customer_user_id" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_class_registrations_status_requested_at", schema: "smart_pool",
            table: "class_registrations", columns: new[] { "status", "requested_at" });
        migrationBuilder.CreateIndex(
            name: "ix_class_registrations_reviewed_by_user_id", schema: "smart_pool",
            table: "class_registrations", column: "reviewed_by_user_id");

        migrationBuilder.AddForeignKey(
            name: "class_attendances_registration_session_fkey", schema: "smart_pool",
            table: "class_attendances", columns: new[] { "registration_id", "session_id" },
            principalSchema: "smart_pool", principalTable: "class_registrations",
            principalColumns: new[] { "id", "session_id" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.CreateIndex(
            name: "ix_class_attendances_session_id", schema: "smart_pool",
            table: "class_attendances", column: "session_id");
        migrationBuilder.CreateIndex(
            name: "ux_class_attendances_registration_session", schema: "smart_pool",
            table: "class_attendances", columns: new[] { "registration_id", "session_id" },
            unique: true);

        migrationBuilder.DropCheckConstraint(
            name: "ck_swim_classes_fee_nonnegative", schema: "smart_pool", table: "swim_classes");
        migrationBuilder.DropCheckConstraint(
            name: "ck_swim_classes_registration_time_order", schema: "smart_pool", table: "swim_classes");
        foreach (var name in new[]
        {
            "fee", "registration_start_at", "registration_end_at"
        })
        {
            migrationBuilder.DropColumn(name: name, schema: "smart_pool", table: "swim_classes");
        }
    }
}
