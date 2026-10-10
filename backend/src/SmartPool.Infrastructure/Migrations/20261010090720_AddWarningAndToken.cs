using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWarningAndToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_incidents",
                schema: "smart_pool",
                table: "incidents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    revoke_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "warnings",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    cam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warning_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    image_url = table.Column<string>(type: "text", nullable: false),
                    severity_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warnings", x => x.id);
                    table.ForeignKey(
                        name: "FK_warnings_equipments_cam_id",
                        column: x => x.cam_id,
                        principalSchema: "smart_pool",
                        principalTable: "equipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warnings_incidents_incident_id",
                        column: x => x.incident_id,
                        principalSchema: "smart_pool",
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_warnings_users_verified_by",
                        column: x => x.verified_by,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_refresh_tokens_token_hash",
                schema: "smart_pool",
                table: "refresh_tokens",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "idx_refresh_tokens_user_id",
                schema: "smart_pool",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_warnings_cam_id",
                schema: "smart_pool",
                table: "warnings",
                column: "cam_id");

            migrationBuilder.CreateIndex(
                name: "idx_warnings_status",
                schema: "smart_pool",
                table: "warnings",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_warnings_time",
                schema: "smart_pool",
                table: "warnings",
                column: "warning_time",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_warnings_incident_id",
                schema: "smart_pool",
                table: "warnings",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_warnings_verified_by",
                schema: "smart_pool",
                table: "warnings",
                column: "verified_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "warnings",
                schema: "smart_pool");

            migrationBuilder.DropColumn(
                name: "image_incidents",
                schema: "smart_pool",
                table: "incidents");
        }
    }
}
