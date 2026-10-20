using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GoodsReceiptsAndStaffAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail with an actionable message before changing legacy data. Never delete
            // duplicate assignments or manufacture missing attendance timestamps.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM smart_pool.employee_shifts
                        GROUP BY employee_id, shift_id, work_date HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Duplicate employee shift assignments exist. Resolve them before applying GoodsReceiptsAndStaffAttendance.';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM smart_pool.employee_shifts
                        WHERE check_out_time IS NOT NULL
                          AND (check_in_time IS NULL OR check_out_time < check_in_time)
                    ) THEN
                        RAISE EXCEPTION 'Invalid employee attendance timestamps exist. Resolve them before applying GoodsReceiptsAndStaffAttendance.';
                    END IF;
                END $$;
                CREATE UNIQUE INDEX IF NOT EXISTS ux_employee_shifts_employee_id_shift_id_work_date
                    ON smart_pool.employee_shifts (employee_id, shift_id, work_date);
                UPDATE smart_pool.roles
                    SET permissions = permissions - 'salaries', updated_at = now()
                    WHERE jsonb_typeof(permissions) = 'object' AND permissions ? 'salaries';
                """);

            migrationBuilder.DropTable(
                name: "salaries",
                schema: "smart_pool");

            migrationBuilder.DropColumn(
                name: "base_salary",
                schema: "smart_pool",
                table: "employees");

            migrationBuilder.AddColumn<Guid>(
                name: "receipt_detail_id",
                schema: "smart_pool",
                table: "inventory_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "goods_receipts",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    receipt_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipts", x => x.id);
                    table.CheckConstraint("ck_goods_receipts_status", "status IN ('Draft', 'Posted', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_goods_receipts_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_details",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt_details", x => x.id);
                    table.CheckConstraint("ck_goods_receipt_details_cost", "unit_cost >= 0");
                    table.CheckConstraint("ck_goods_receipt_details_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_goods_receipt_details_goods_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalSchema: "smart_pool",
                        principalTable: "goods_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipt_details_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "smart_pool",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_inventory_logs_receipt_detail_id",
                schema: "smart_pool",
                table: "inventory_logs",
                column: "receipt_detail_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_employee_shifts_checkout_requires_checkin",
                schema: "smart_pool",
                table: "employee_shifts",
                sql: "check_out_time IS NULL OR check_in_time IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_employee_shifts_time_order",
                schema: "smart_pool",
                table: "employee_shifts",
                sql: "check_out_time IS NULL OR check_out_time >= check_in_time");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_details_product_id",
                schema: "smart_pool",
                table: "goods_receipt_details",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_details_receipt_id_product_id",
                schema: "smart_pool",
                table: "goods_receipt_details",
                columns: new[] { "receipt_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_created_by",
                schema: "smart_pool",
                table: "goods_receipts",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_goods_receipts_received_at",
                schema: "smart_pool",
                table: "goods_receipts",
                column: "received_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ux_goods_receipts_code",
                schema: "smart_pool",
                table: "goods_receipts",
                column: "receipt_code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_logs_goods_receipt_details_receipt_detail_id",
                schema: "smart_pool",
                table: "inventory_logs",
                column: "receipt_detail_id",
                principalSchema: "smart_pool",
                principalTable: "goods_receipt_details",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_logs_goods_receipt_details_receipt_detail_id",
                schema: "smart_pool",
                table: "inventory_logs");

            migrationBuilder.DropTable(
                name: "goods_receipt_details",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "goods_receipts",
                schema: "smart_pool");

            migrationBuilder.DropIndex(
                name: "ux_inventory_logs_receipt_detail_id",
                schema: "smart_pool",
                table: "inventory_logs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_employee_shifts_checkout_requires_checkin",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_employee_shifts_time_order",
                schema: "smart_pool",
                table: "employee_shifts");

            migrationBuilder.DropColumn(
                name: "receipt_detail_id",
                schema: "smart_pool",
                table: "inventory_logs");

            migrationBuilder.AddColumn<decimal>(
                name: "base_salary",
                schema: "smart_pool",
                table: "employees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "salaries",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bonus = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true, defaultValueSql: "0"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    deduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true, defaultValueSql: "0"),
                    month = table.Column<int>(type: "integer", nullable: false),
                    net_salary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_shifts = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    year = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("salaries_pkey", x => x.id);
                    table.ForeignKey(
                        name: "salaries_employee_id_fkey",
                        column: x => x.employee_id,
                        principalSchema: "smart_pool",
                        principalTable: "employees",
                        principalColumn: "user_id");
                });

            migrationBuilder.CreateIndex(
                name: "ux_salaries_employee_id_month_year",
                schema: "smart_pool",
                table: "salaries",
                columns: new[] { "employee_id", "month", "year" },
                unique: true);
        }
    }
}
