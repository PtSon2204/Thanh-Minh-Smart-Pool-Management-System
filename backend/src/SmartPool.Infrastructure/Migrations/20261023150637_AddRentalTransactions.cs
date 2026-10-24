using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "fee_amount",
                schema: "smart_pool",
                table: "rentals",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "product_name_snapshot",
                schema: "smart_pool",
                table: "rentals",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "resolved_at_utc",
                schema: "smart_pool",
                table: "rentals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "settlement_id",
                schema: "smart_pool",
                table: "rentals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rental_transactions",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fee_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    retained_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    confirmed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    result_json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rental_transactions_pkey", x => x.id);
                    table.CheckConstraint("ck_rental_transactions_amounts", "(kind = 'Collection' AND refund_amount = 0 AND retained_amount = 0 AND product_id IS NULL) OR (kind = 'Settlement' AND fee_amount = 0 AND deposit_amount = refund_amount + retained_amount AND product_id IS NOT NULL)");
                    table.CheckConstraint("ck_rental_transactions_kind", "kind IN ('Collection', 'Settlement')");
                    table.CheckConstraint("ck_rental_transactions_nonnegative", "fee_amount >= 0 AND deposit_amount >= 0 AND refund_amount >= 0 AND retained_amount >= 0");
                    table.ForeignKey(
                        name: "FK_rental_transactions_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "smart_pool",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_transactions_payments_payment_id",
                        column: x => x.payment_id,
                        principalSchema: "smart_pool",
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_transactions_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "smart_pool",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_transactions_users_confirmed_by_user_id",
                        column: x => x.confirmed_by_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rentals_settlement_id",
                schema: "smart_pool",
                table: "rentals",
                column: "settlement_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rentals_issued_snapshot",
                schema: "smart_pool",
                table: "rentals",
                sql: "fee_amount IS NULL OR (fee_amount >= 0 AND deposit_amount = 20000 AND product_name_snapshot IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_rental_transactions_confirmed_by_user_id",
                schema: "smart_pool",
                table: "rental_transactions",
                column: "confirmed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_rental_transactions_order_time",
                schema: "smart_pool",
                table: "rental_transactions",
                columns: new[] { "order_id", "confirmed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_rental_transactions_payment_id",
                schema: "smart_pool",
                table: "rental_transactions",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_rental_transactions_product_id",
                schema: "smart_pool",
                table: "rental_transactions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ux_rental_transactions_collection_order",
                schema: "smart_pool",
                table: "rental_transactions",
                column: "order_id",
                unique: true,
                filter: "kind = 'Collection'");

            migrationBuilder.AddForeignKey(
                name: "FK_rentals_rental_transactions_settlement_id",
                schema: "smart_pool",
                table: "rentals",
                column: "settlement_id",
                principalSchema: "smart_pool",
                principalTable: "rental_transactions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rentals_rental_transactions_settlement_id",
                schema: "smart_pool",
                table: "rentals");

            migrationBuilder.DropTable(
                name: "rental_transactions",
                schema: "smart_pool");

            migrationBuilder.DropIndex(
                name: "IX_rentals_settlement_id",
                schema: "smart_pool",
                table: "rentals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rentals_issued_snapshot",
                schema: "smart_pool",
                table: "rentals");

            migrationBuilder.DropColumn(
                name: "fee_amount",
                schema: "smart_pool",
                table: "rentals");

            migrationBuilder.DropColumn(
                name: "product_name_snapshot",
                schema: "smart_pool",
                table: "rentals");

            migrationBuilder.DropColumn(
                name: "resolved_at_utc",
                schema: "smart_pool",
                table: "rentals");

            migrationBuilder.DropColumn(
                name: "settlement_id",
                schema: "smart_pool",
                table: "rentals");

        }
    }
}
