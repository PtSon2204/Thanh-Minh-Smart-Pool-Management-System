using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSafetyMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                schema: "smart_pool",
                table: "warnings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "cam_id",
                schema: "smart_pool",
                table: "warnings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "risk_episode",
                schema: "smart_pool",
                table: "warnings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "snapshot_attempts",
                schema: "smart_pool",
                table: "warnings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "snapshot_status",
                schema: "smart_pool",
                table: "warnings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NONE");

            migrationBuilder.AddColumn<string>(
                name: "source_type",
                schema: "smart_pool",
                table: "warnings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "LIVE");

            migrationBuilder.AddColumn<string>(
                name: "track_id",
                schema: "smart_pool",
                table: "warnings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "video_job_id",
                schema: "smart_pool",
                table: "warnings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "video_timestamp",
                schema: "smart_pool",
                table: "warnings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "camera_id",
                schema: "smart_pool",
                table: "incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "location",
                schema: "smart_pool",
                table: "incidents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "warning_id",
                schema: "smart_pool",
                table: "incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "video_analysis_jobs",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    original_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    input_file = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    output_file = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    progress = table.Column<double>(type: "double precision", nullable: false),
                    error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_analysis_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_video_analysis_jobs_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_warnings_source_type_risk_episode",
                schema: "smart_pool",
                table: "warnings",
                columns: new[] { "source_type", "risk_episode" },
                unique: true,
                filter: "risk_episode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_warnings_video_job_id",
                schema: "smart_pool",
                table: "warnings",
                column: "video_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_camera_id",
                schema: "smart_pool",
                table: "incidents",
                column: "camera_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_warning_id",
                schema: "smart_pool",
                table: "incidents",
                column: "warning_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_analysis_jobs_status_created_at",
                schema: "smart_pool",
                table: "video_analysis_jobs",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_video_analysis_jobs_uploaded_by",
                schema: "smart_pool",
                table: "video_analysis_jobs",
                column: "uploaded_by");







            migrationBuilder.AddForeignKey(
                name: "FK_incidents_equipments_camera_id",
                schema: "smart_pool",
                table: "incidents",
                column: "camera_id",
                principalSchema: "smart_pool",
                principalTable: "equipments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_warnings_video_analysis_jobs_video_job_id",
                schema: "smart_pool",
                table: "warnings",
                column: "video_job_id",
                principalSchema: "smart_pool",
                principalTable: "video_analysis_jobs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_incidents_warnings_warning_id",
                schema: "smart_pool", table: "incidents", column: "warning_id",
                principalSchema: "smart_pool", principalTable: "warnings", principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Uploaded alerts have no camera. Refuse a lossy rollback rather than insert invalid camera UUIDs.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM smart_pool.warnings WHERE cam_id IS NULL) THEN
                        RAISE EXCEPTION 'Cannot roll back safety monitoring while uploaded-video alerts exist.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(name: "FK_incidents_warnings_warning_id", schema: "smart_pool", table: "incidents");






            migrationBuilder.DropForeignKey(
                name: "FK_incidents_equipments_camera_id",
                schema: "smart_pool",
                table: "incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_warnings_video_analysis_jobs_video_job_id",
                schema: "smart_pool",
                table: "warnings");

            migrationBuilder.DropTable(
                name: "video_analysis_jobs",
                schema: "smart_pool");

            migrationBuilder.DropIndex(
                name: "IX_warnings_source_type_risk_episode",
                schema: "smart_pool",
                table: "warnings");

            migrationBuilder.DropIndex(
                name: "IX_warnings_video_job_id",
                schema: "smart_pool",
                table: "warnings");

            migrationBuilder.DropIndex(
                name: "IX_incidents_camera_id",
                schema: "smart_pool",
                table: "incidents");

            migrationBuilder.DropIndex(
                name: "IX_incidents_warning_id",
                schema: "smart_pool",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "risk_episode",
                schema: "smart_pool",
                table: "warnings");

            migrationBuilder.DropColumn(
                name: "snapshot_attempts",
                schema: "smart_pool",
                table: "warnings");

            migrationBuilder.DropColumn(
                name: "snapshot_status",
                schema: "smart_pool",
                table: "warnings");

            migrationBuilder.DropColumn(
                name: "source_type",
                schema: "smart_pool",
                table: "warnings");

            migrationBuilder.DropColumn(
                name: "track_id",
                schema: "smart_pool",
                table: "warnings");

            migrationBuilder.DropColumn(
                name: "video_job_id",
                schema: "smart_pool",
                table: "warnings");

            migrationBuilder.DropColumn(
                name: "video_timestamp",
                schema: "smart_pool",
                table: "warnings");

            migrationBuilder.DropColumn(
                name: "camera_id",
                schema: "smart_pool",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "location",
                schema: "smart_pool",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "warning_id",
                schema: "smart_pool",
                table: "incidents");

            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                schema: "smart_pool",
                table: "warnings",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "cam_id",
                schema: "smart_pool",
                table: "warnings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
