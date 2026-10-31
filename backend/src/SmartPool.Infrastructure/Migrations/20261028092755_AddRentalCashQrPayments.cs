using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalCashQrPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "refund_method",
                schema: "smart_pool",
                table: "rental_transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bank_transfer_receipts",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_event_id = table.Column<long>(type: "bigint", nullable: false),
                    financial_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    bank_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    disposition = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    needs_reconciliation = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("bank_transfer_receipts_pkey", x => x.id);
                    table.CheckConstraint("ck_bank_transfer_receipts_amount", "amount > 0");
                    table.CheckConstraint("ck_bank_transfer_receipts_direction", "direction = 'in'");
                    table.ForeignKey(
                        name: "FK_bank_transfer_receipts_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "smart_pool",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rental_payment_intents",
                schema: "smart_pool",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    initiated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lines_json = table.Column<string>(type: "text", nullable: false),
                    fee_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    issued_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("rental_payment_intents_pkey", x => x.id);
                    table.CheckConstraint("ck_rental_payment_intents_nonnegative", "fee_amount >= 0 AND deposit_amount >= 0");
                    table.CheckConstraint("ck_rental_payment_intents_status", "status IN ('PENDING', 'PARTIAL', 'PAID', 'ISSUED', 'EXPIRED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "FK_rental_payment_intents_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "smart_pool",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_payment_intents_users_initiated_by_user_id",
                        column: x => x.initiated_by_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_payment_intents_users_issued_by_user_id",
                        column: x => x.issued_by_user_id,
                        principalSchema: "smart_pool",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_rental_transactions_refund_method",
                schema: "smart_pool",
                table: "rental_transactions",
                sql: "refund_method IS NULL OR refund_method = 'CASH'");

            migrationBuilder.CreateIndex(
                name: "ix_bank_transfer_receipts_financial_hash",
                schema: "smart_pool",
                table: "bank_transfer_receipts",
                column: "financial_hash");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transfer_receipts_order_id",
                schema: "smart_pool",
                table: "bank_transfer_receipts",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_transfer_receipts_reference",
                schema: "smart_pool",
                table: "bank_transfer_receipts",
                column: "reference");

            migrationBuilder.CreateIndex(
                name: "ux_bank_transfer_receipts_provider_event",
                schema: "smart_pool",
                table: "bank_transfer_receipts",
                columns: new[] { "provider", "provider_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rental_payment_intents_initiated_by_user_id",
                schema: "smart_pool",
                table: "rental_payment_intents",
                column: "initiated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_rental_payment_intents_issued_by_user_id",
                schema: "smart_pool",
                table: "rental_payment_intents",
                column: "issued_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_rental_payment_intents_status_expiry",
                schema: "smart_pool",
                table: "rental_payment_intents",
                columns: new[] { "status", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_rental_payment_intents_order",
                schema: "smart_pool",
                table: "rental_payment_intents",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rental_payment_intents_reference",
                schema: "smart_pool",
                table: "rental_payment_intents",
                column: "reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_transfer_receipts",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "rental_payment_intents",
                schema: "smart_pool");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rental_transactions_refund_method",
                schema: "smart_pool",
                table: "rental_transactions");

            migrationBuilder.DropColumn(
                name: "refund_method",
                schema: "smart_pool",
                table: "rental_transactions");
        }
    }
}
