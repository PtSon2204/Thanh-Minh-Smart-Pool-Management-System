using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassRegistrationCancellationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancel_reason",
                schema: "smart_pool",
                table: "class_registrations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by_user_id",
                schema: "smart_pool",
                table: "class_registrations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_class_registrations_cancelled_by_user_id",
                schema: "smart_pool",
                table: "class_registrations",
                column: "cancelled_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "class_registrations_cancelled_by_user_id_fkey",
                schema: "smart_pool",
                table: "class_registrations",
                column: "cancelled_by_user_id",
                principalSchema: "smart_pool",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "class_registrations_cancelled_by_user_id_fkey",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.DropIndex(
                name: "IX_class_registrations_cancelled_by_user_id",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.DropColumn(
                name: "cancel_reason",
                schema: "smart_pool",
                table: "class_registrations");

            migrationBuilder.DropColumn(
                name: "cancelled_by_user_id",
                schema: "smart_pool",
                table: "class_registrations");
        }
    }
}
