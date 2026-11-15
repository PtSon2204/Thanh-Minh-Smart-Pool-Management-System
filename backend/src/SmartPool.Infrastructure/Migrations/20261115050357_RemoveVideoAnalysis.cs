using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVideoAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.DropColumn(
                name: "source_type",
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

            migrationBuilder.CreateIndex(
                name: "IX_warnings_risk_episode",
                schema: "smart_pool",
                table: "warnings",
                column: "risk_episode",
                unique: true,
                filter: "risk_episode IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_warnings_risk_episode",
                schema: "smart_pool",
                table: "warnings");

            migrationBuilder.AddColumn<string>(
                name: "source_type",
                schema: "smart_pool",
                table: "warnings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "LIVE");

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

            migrationBuilder.CreateTable(
                name: "video_analysis_jobs",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    input_file = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    original_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    output_file = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    progress = table.Column<double>(type: "double precision", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                name: "FK_warnings_video_analysis_jobs_video_job_id",
                schema: "smart_pool",
                table: "warnings",
                column: "video_job_id",
                principalSchema: "smart_pool",
                principalTable: "video_analysis_jobs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
