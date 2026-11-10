using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassRegistrationApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_class_registrations_status",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.AddColumn<string>(
                name: "review_note",
                schema: "smart_pool",
                table: "class_registrations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                schema: "smart_pool",
                table: "class_registrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by_user_id",
                schema: "smart_pool",
                table: "class_registrations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_registrations_reviewed_by_user_id",
                schema: "smart_pool",
                table: "class_registrations",
                column: "reviewed_by_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_class_registrations_pending_fields",
                schema: "smart_pool",
                table: "class_registrations",
                sql: "status <> 'PendingApproval' OR (confirmed_at IS NULL AND reviewed_by_user_id IS NULL AND reviewed_at IS NULL AND review_note IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_class_registrations_rejected_fields",
                schema: "smart_pool",
                table: "class_registrations",
                sql: "status <> 'Rejected' OR (confirmed_at IS NULL AND reviewed_by_user_id IS NOT NULL AND reviewed_at IS NOT NULL AND review_note IS NOT NULL AND btrim(review_note) <> '')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_class_registrations_review_fields_pair",
                schema: "smart_pool",
                table: "class_registrations",
                sql: "(reviewed_by_user_id IS NULL) = (reviewed_at IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_class_registrations_reviewed_after_registered",
                schema: "smart_pool",
                table: "class_registrations",
                sql: "reviewed_at IS NULL OR reviewed_at >= registered_at");

            migrationBuilder.AddCheckConstraint(
                name: "ck_class_registrations_status",
                schema: "smart_pool",
                table: "class_registrations",
                sql: "status IN ('PendingApproval', 'Confirmed', 'Rejected', 'Cancelled')");

            migrationBuilder.AddForeignKey(
                name: "class_registrations_reviewed_by_user_id_fkey",
                schema: "smart_pool",
                table: "class_registrations",
                column: "reviewed_by_user_id",
                principalSchema: "smart_pool",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "class_registrations_reviewed_by_user_id_fkey",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.DropIndex(
                name: "ix_class_registrations_reviewed_by_user_id",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_class_registrations_pending_fields",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_class_registrations_rejected_fields",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_class_registrations_review_fields_pair",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_class_registrations_reviewed_after_registered",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_class_registrations_status",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.DropColumn(
                name: "review_note",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.DropColumn(
                name: "reviewed_by_user_id",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_class_registrations_status",
                schema: "smart_pool",
                table: "class_registrations",
                sql: "status IN ('Confirmed', 'Cancelled')");
        }
    }
}
