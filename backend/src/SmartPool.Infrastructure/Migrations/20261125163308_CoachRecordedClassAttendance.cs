using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoachRecordedClassAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "class_attendances_reviewed_by_user_id_fkey",
                schema: "smart_pool",
                table: "class_attendances");
            migrationBuilder.DropCheckConstraint(
                name: "ck_class_attendances_review_fields_pair",
                schema: "smart_pool",
                table: "class_attendances");
            migrationBuilder.DropIndex(
                name: "ix_class_attendances_reviewed_by_user_id",
                schema: "smart_pool",
                table: "class_attendances");
            migrationBuilder.DropIndex(
                name: "ix_class_attendances_status_submitted_at",
                schema: "smart_pool",
                table: "class_attendances");
            migrationBuilder.DropColumn(name: "reviewed_by_user_id", schema: "smart_pool", table: "class_attendances");
            migrationBuilder.DropColumn(name: "reviewed_at", schema: "smart_pool", table: "class_attendances");
            migrationBuilder.DropColumn(name: "review_note", schema: "smart_pool", table: "class_attendances");
            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                schema: "smart_pool",
                table: "class_attendances",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);
            migrationBuilder.AddCheckConstraint(
                name: "ck_class_attendances_status_image", schema: "smart_pool",
                table: "class_attendances",
                sql: "(status <> 'Verified' OR image_url IS NOT NULL) AND (status <> 'Absent' OR image_url IS NULL)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_class_attendances_status_image", schema: "smart_pool",
                table: "class_attendances");
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM smart_pool.class_attendances WHERE image_url IS NULL) THEN
                        RAISE EXCEPTION 'Cannot restore non-null image_url while absent attendance records exist';
                    END IF;
                END $$;
                """);
            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                schema: "smart_pool",
                table: "class_attendances",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048,
                oldNullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by_user_id", schema: "smart_pool", table: "class_attendances",
                type: "uuid", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at", schema: "smart_pool", table: "class_attendances",
                type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "review_note", schema: "smart_pool", table: "class_attendances",
                type: "character varying(500)", maxLength: 500, nullable: true);
            migrationBuilder.CreateIndex(
                name: "ix_class_attendances_reviewed_by_user_id", schema: "smart_pool",
                table: "class_attendances", column: "reviewed_by_user_id");
            migrationBuilder.CreateIndex(
                name: "ix_class_attendances_status_submitted_at", schema: "smart_pool",
                table: "class_attendances", columns: new[] { "status", "submitted_at" });
            migrationBuilder.AddForeignKey(
                name: "class_attendances_reviewed_by_user_id_fkey", schema: "smart_pool",
                table: "class_attendances", column: "reviewed_by_user_id",
                principalSchema: "smart_pool", principalTable: "users", principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
            migrationBuilder.AddCheckConstraint(
                name: "ck_class_attendances_review_fields_pair", schema: "smart_pool",
                table: "class_attendances",
                sql: "(reviewed_by_user_id IS NULL) = (reviewed_at IS NULL)");
        }
    }
}
