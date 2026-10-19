START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    DROP TABLE smart_pool.salaries;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    ALTER TABLE smart_pool.employees DROP COLUMN base_salary;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    ALTER TABLE smart_pool.inventory_logs ADD receipt_detail_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    CREATE TABLE smart_pool.goods_receipts (
        id uuid NOT NULL DEFAULT (uuid_generate_v4()),
        receipt_code character varying(50) NOT NULL,
        received_at timestamp with time zone NOT NULL,
        supplier_name character varying(255),
        status character varying(20) NOT NULL DEFAULT 'Draft',
        created_by uuid NOT NULL,
        note text,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_goods_receipts" PRIMARY KEY (id),
        CONSTRAINT ck_goods_receipts_status CHECK (status IN ('Draft', 'Posted', 'Cancelled')),
        CONSTRAINT "FK_goods_receipts_users_created_by" FOREIGN KEY (created_by) REFERENCES smart_pool.users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    CREATE TABLE smart_pool.goods_receipt_details (
        id uuid NOT NULL DEFAULT (uuid_generate_v4()),
        receipt_id uuid NOT NULL,
        product_id uuid NOT NULL,
        quantity integer NOT NULL,
        unit_cost numeric(18,2) NOT NULL,
        CONSTRAINT "PK_goods_receipt_details" PRIMARY KEY (id),
        CONSTRAINT ck_goods_receipt_details_cost CHECK (unit_cost >= 0),
        CONSTRAINT ck_goods_receipt_details_quantity CHECK (quantity > 0),
        CONSTRAINT "FK_goods_receipt_details_goods_receipts_receipt_id" FOREIGN KEY (receipt_id) REFERENCES smart_pool.goods_receipts (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_goods_receipt_details_products_product_id" FOREIGN KEY (product_id) REFERENCES smart_pool.products (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    CREATE UNIQUE INDEX ux_inventory_logs_receipt_detail_id ON smart_pool.inventory_logs (receipt_detail_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    ALTER TABLE smart_pool.employee_shifts ADD CONSTRAINT ck_employee_shifts_checkout_requires_checkin CHECK (check_out_time IS NULL OR check_in_time IS NOT NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    ALTER TABLE smart_pool.employee_shifts ADD CONSTRAINT ck_employee_shifts_time_order CHECK (check_out_time IS NULL OR check_out_time >= check_in_time);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    CREATE INDEX "IX_goods_receipt_details_product_id" ON smart_pool.goods_receipt_details (product_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    CREATE UNIQUE INDEX "IX_goods_receipt_details_receipt_id_product_id" ON smart_pool.goods_receipt_details (receipt_id, product_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    CREATE INDEX "IX_goods_receipts_created_by" ON smart_pool.goods_receipts (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    CREATE INDEX ix_goods_receipts_received_at ON smart_pool.goods_receipts (received_at DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    CREATE UNIQUE INDEX ux_goods_receipts_code ON smart_pool.goods_receipts (receipt_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    ALTER TABLE smart_pool.inventory_logs ADD CONSTRAINT "FK_inventory_logs_goods_receipt_details_receipt_detail_id" FOREIGN KEY (receipt_detail_id) REFERENCES smart_pool.goods_receipt_details (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019063413_GoodsReceiptsAndStaffAttendance') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019063413_GoodsReceiptsAndStaffAttendance', '8.0.11');
    END IF;
END $EF$;
COMMIT;

