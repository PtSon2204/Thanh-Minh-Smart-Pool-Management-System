using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSwimClassManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "swim_classes",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    coach_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("swim_classes_pkey", x => x.id);
                    table.CheckConstraint("ck_swim_classes_capacity_positive", "capacity > 0");
                    table.ForeignKey(
                        name: "swim_classes_coach_user_id_fkey",
                        column: x => x.coach_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "swim_classes_created_by_user_id_fkey",
                        column: x => x.created_by_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_sessions",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coach_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("class_sessions_pkey", x => x.id);
                    table.CheckConstraint("ck_class_sessions_time_order", "starts_at < ends_at");
                    table.ForeignKey(
                        name: "class_sessions_class_id_fkey",
                        column: x => x.class_id,
                        principalSchema: "smart_pool",
                        principalTable: "swim_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "class_sessions_coach_user_id_fkey",
                        column: x => x.coach_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_registrations",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("class_registrations_pkey", x => x.id);
                    table.UniqueConstraint("ak_class_registrations_id_session_id", x => new { x.id, x.session_id });
                    table.CheckConstraint("ck_class_registrations_review_fields_pair", "(reviewed_by_user_id IS NULL) = (reviewed_at IS NULL)");
                    table.ForeignKey(
                        name: "class_registrations_customer_user_id_fkey",
                        column: x => x.customer_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "class_registrations_reviewed_by_user_id_fkey",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "class_registrations_session_id_fkey",
                        column: x => x.session_id,
                        principalSchema: "smart_pool",
                        principalTable: "class_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_attendances",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    registration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("class_attendances_pkey", x => x.id);
                    table.CheckConstraint("ck_class_attendances_review_fields_pair", "(reviewed_by_user_id IS NULL) = (reviewed_at IS NULL)");
                    table.ForeignKey(
                        name: "class_attendances_registration_session_fkey",
                        columns: x => new { x.registration_id, x.session_id },
                        principalSchema: "smart_pool",
                        principalTable: "class_registrations",
                        principalColumns: new[] { "id", "session_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "class_attendances_reviewed_by_user_id_fkey",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "class_attendances_session_id_fkey",
                        column: x => x.session_id,
                        principalSchema: "smart_pool",
                        principalTable: "class_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "class_attendances_submitted_by_user_id_fkey",
                        column: x => x.submitted_by_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_class_attendances_reviewed_by_user_id",
                schema: "smart_pool",
                table: "class_attendances",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_attendances_session_id",
                schema: "smart_pool",
                table: "class_attendances",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_attendances_status_submitted_at",
                schema: "smart_pool",
                table: "class_attendances",
                columns: new[] { "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_class_attendances_submitted_by_user_id",
                schema: "smart_pool",
                table: "class_attendances",
                column: "submitted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_class_attendances_registration_session",
                schema: "smart_pool",
                table: "class_attendances",
                columns: new[] { "registration_id", "session_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_registrations_customer_status",
                schema: "smart_pool",
                table: "class_registrations",
                columns: new[] { "customer_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_class_registrations_reviewed_by_user_id",
                schema: "smart_pool",
                table: "class_registrations",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_registrations_status_requested_at",
                schema: "smart_pool",
                table: "class_registrations",
                columns: new[] { "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ux_class_registrations_session_customer",
                schema: "smart_pool",
                table: "class_registrations",
                columns: new[] { "session_id", "customer_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_sessions_class_starts_at",
                schema: "smart_pool",
                table: "class_sessions",
                columns: new[] { "class_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_class_sessions_coach_starts_at",
                schema: "smart_pool",
                table: "class_sessions",
                columns: new[] { "coach_user_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_swim_classes_coach_user_id",
                schema: "smart_pool",
                table: "swim_classes",
                column: "coach_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_swim_classes_created_by_user_id",
                schema: "smart_pool",
                table: "swim_classes",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_swim_classes_status",
                schema: "smart_pool",
                table: "swim_classes",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "class_attendances",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "class_registrations",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "class_sessions",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "swim_classes",
                schema: "smart_pool");
        }
    }
}
