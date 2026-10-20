using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGoodsPurchasingAndRetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retire retail items before removing the discriminator. Keep their identities
            // so historical order details and rentals still resolve the original item.
            migrationBuilder.Sql("""
                UPDATE smart_pool.products
                SET is_active = false, is_deleted = true, updated_at = now()
                WHERE lower(btrim(type)) IS DISTINCT FROM 'rental';

                UPDATE smart_pool.products
                SET stock_quantity = 0
                WHERE stock_quantity IS NULL AND lower(btrim(type)) = 'rental';

                DELETE FROM smart_pool.inventory_logs
                WHERE receipt_detail_id IS NOT NULL OR change_type = 'GoodsReceipt';
                """);

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

            migrationBuilder.DropColumn(
                name: "type",
                schema: "smart_pool",
                table: "products");

            migrationBuilder.DropColumn(
                name: "receipt_detail_id",
                schema: "smart_pool",
                table: "inventory_logs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "smart_pool",
                table: "products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Rental");

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
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    note = table.Column<string>(type: "text", nullable: true),
                    receipt_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    supplier_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
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
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
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
    }
}
