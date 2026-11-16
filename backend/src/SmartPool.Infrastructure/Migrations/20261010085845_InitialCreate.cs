using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartPool.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $EF$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'smart_pool') THEN
                        CREATE SCHEMA smart_pool;
                    END IF;
                END $EF$;

                CREATE EXTENSION IF NOT EXISTS pgcrypto;
                DO $EF$
                BEGIN
                    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'smart_pool') THEN
                        CREATE SCHEMA smart_pool;
                    END IF;
                END $EF$;

                CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

                CREATE TABLE smart_pool.equipments (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    category character varying(50) NOT NULL,
                    created_at timestamp with time zone DEFAULT (now()),
                    is_deleted boolean DEFAULT FALSE,
                    location character varying(255),
                    metadata jsonb,
                    name character varying(255) NOT NULL,
                    status character varying(50) DEFAULT ('Active'::character varying),
                    updated_at timestamp with time zone DEFAULT (now()),
                    CONSTRAINT equipments_pkey PRIMARY KEY (id)
                );

                CREATE TABLE smart_pool.products (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    created_at timestamp with time zone DEFAULT (now()),
                    is_active boolean DEFAULT TRUE,
                    is_deleted boolean DEFAULT FALSE,
                    name character varying(255) NOT NULL,
                    price numeric(18,2) NOT NULL,
                    row_version uuid DEFAULT (uuid_generate_v4()),
                    stock_quantity integer DEFAULT 0,
                    type character varying(50) NOT NULL,
                    updated_at timestamp with time zone DEFAULT (now()),
                    CONSTRAINT products_pkey PRIMARY KEY (id)
                );

                CREATE TABLE smart_pool.roles (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    created_at timestamp with time zone DEFAULT (now()),
                    is_deleted boolean DEFAULT FALSE,
                    name character varying(100) NOT NULL,
                    permissions jsonb,
                    updated_at timestamp with time zone DEFAULT (now()),
                    CONSTRAINT roles_pkey PRIMARY KEY (id)
                );

                CREATE TABLE smart_pool.shifts (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    end_time time without time zone NOT NULL,
                    is_active boolean DEFAULT TRUE,
                    name character varying(100) NOT NULL,
                    start_time time without time zone NOT NULL,
                    CONSTRAINT shifts_pkey PRIMARY KEY (id)
                );

                CREATE TABLE smart_pool.ticket_types (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    created_at timestamp with time zone DEFAULT (now()),
                    duration_days integer,
                    is_active boolean DEFAULT TRUE,
                    is_deleted boolean DEFAULT FALSE,
                    name character varying(255) NOT NULL,
                    price numeric(18,2) NOT NULL,
                    ticket_category character varying(50) NOT NULL,
                    updated_at timestamp with time zone DEFAULT (now()),
                    CONSTRAINT ticket_types_pkey PRIMARY KEY (id)
                );

                CREATE TABLE smart_pool.vouchers (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    code character varying(50) NOT NULL,
                    created_at timestamp with time zone DEFAULT (now()),
                    discount_type character varying(20) NOT NULL,
                    discount_value numeric(18,2) NOT NULL,
                    end_date timestamp with time zone,
                    is_active boolean DEFAULT TRUE,
                    is_deleted boolean DEFAULT FALSE,
                    min_order_value numeric(18,2) DEFAULT (0),
                    start_date timestamp with time zone,
                    CONSTRAINT vouchers_pkey PRIMARY KEY (id)
                );

                CREATE TABLE smart_pool.users (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    role_id uuid,
                    created_at timestamp with time zone DEFAULT (now()),
                    email character varying(150),
                    is_deleted boolean DEFAULT FALSE,
                    password_hash character varying(255),
                    phone character varying(20),
                    status character varying(50) DEFAULT ('Active'::character varying),
                    updated_at timestamp with time zone DEFAULT (now()),
                    username character varying(100),
                    CONSTRAINT users_pkey PRIMARY KEY (id),
                    CONSTRAINT users_role_id_fkey FOREIGN KEY (role_id) REFERENCES smart_pool.roles (id)
                );

                CREATE TABLE smart_pool.employees (
                    user_id uuid NOT NULL,
                    base_salary numeric(18,2) NOT NULL,
                    created_at timestamp with time zone DEFAULT (now()),
                    join_date date NOT NULL,
                    status character varying(50) DEFAULT ('Working'::character varying),
                    updated_at timestamp with time zone DEFAULT (now()),
                    CONSTRAINT employees_pkey PRIMARY KEY (user_id),
                    CONSTRAINT employees_user_id_fkey FOREIGN KEY (user_id) REFERENCES smart_pool.users (id)
                );

                CREATE TABLE smart_pool.incidents (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    reported_by uuid,
                    created_at timestamp with time zone DEFAULT (now()),
                    description text,
                    incident_time timestamp with time zone DEFAULT (now()),
                    severity_level character varying(50) NOT NULL,
                    status character varying(50) DEFAULT ('Pending'::character varying),
                    title character varying(255) NOT NULL,
                    updated_at timestamp with time zone DEFAULT (now()),
                    CONSTRAINT incidents_pkey PRIMARY KEY (id),
                    CONSTRAINT incidents_reported_by_fkey FOREIGN KEY (reported_by) REFERENCES smart_pool.users (id)
                );

                CREATE TABLE smart_pool.inventory_logs (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    created_by uuid,
                    product_id uuid NOT NULL,
                    change_type character varying(50) NOT NULL,
                    created_at timestamp with time zone DEFAULT (now()),
                    note text,
                    quantity integer NOT NULL,
                    CONSTRAINT inventory_logs_pkey PRIMARY KEY (id),
                    CONSTRAINT inventory_logs_created_by_fkey FOREIGN KEY (created_by) REFERENCES smart_pool.users (id),
                    CONSTRAINT inventory_logs_product_id_fkey FOREIGN KEY (product_id) REFERENCES smart_pool.products (id)
                );

                CREATE TABLE smart_pool.maintenance_schedules (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    assigned_to uuid,
                    equipment_id uuid NOT NULL,
                    created_at timestamp with time zone DEFAULT (now()),
                    description text,
                    planned_date date NOT NULL,
                    status character varying(50) DEFAULT ('Pending'::character varying),
                    updated_at timestamp with time zone DEFAULT (now()),
                    CONSTRAINT maintenance_schedules_pkey PRIMARY KEY (id),
                    CONSTRAINT maintenance_schedules_assigned_to_fkey FOREIGN KEY (assigned_to) REFERENCES smart_pool.users (id),
                    CONSTRAINT maintenance_schedules_equipment_id_fkey FOREIGN KEY (equipment_id) REFERENCES smart_pool.equipments (id)
                );

                CREATE TABLE smart_pool.notifications (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    user_id uuid NOT NULL,
                    content text NOT NULL,
                    created_at timestamp with time zone DEFAULT (now()),
                    is_read boolean DEFAULT FALSE,
                    title character varying(255) NOT NULL,
                    type character varying(50) NOT NULL,
                    CONSTRAINT notifications_pkey PRIMARY KEY (id),
                    CONSTRAINT notifications_user_id_fkey FOREIGN KEY (user_id) REFERENCES smart_pool.users (id)
                );

                CREATE TABLE smart_pool.tickets (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    ticket_type_id uuid NOT NULL,
                    user_id uuid,
                    created_at timestamp with time zone DEFAULT (now()),
                    expiry_date timestamp with time zone,
                    is_deleted boolean DEFAULT FALSE,
                    issue_date timestamp with time zone DEFAULT (now()),
                    qr_code character varying(255) NOT NULL,
                    row_version uuid DEFAULT (uuid_generate_v4()),
                    status character varying(50) DEFAULT ('Active'::character varying),
                    updated_at timestamp with time zone DEFAULT (now()),
                    CONSTRAINT tickets_pkey PRIMARY KEY (id),
                    CONSTRAINT tickets_ticket_type_id_fkey FOREIGN KEY (ticket_type_id) REFERENCES smart_pool.ticket_types (id),
                    CONSTRAINT tickets_user_id_fkey FOREIGN KEY (user_id) REFERENCES smart_pool.users (id)
                );

                CREATE TABLE smart_pool.user_profiles (
                    user_id uuid NOT NULL,
                    address text,
                    avatar_url text,
                    date_of_birth date,
                    full_name character varying(255) NOT NULL,
                    updated_at timestamp with time zone DEFAULT (now()),
                    CONSTRAINT user_profiles_pkey PRIMARY KEY (user_id),
                    CONSTRAINT user_profiles_user_id_fkey FOREIGN KEY (user_id) REFERENCES smart_pool.users (id)
                );

                CREATE TABLE smart_pool.employee_shifts (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    employee_id uuid NOT NULL,
                    shift_id uuid NOT NULL,
                    check_in_time timestamp with time zone,
                    check_out_time timestamp with time zone,
                    created_at timestamp with time zone DEFAULT (now()),
                    status character varying(50) DEFAULT ('Scheduled'::character varying),
                    updated_at timestamp with time zone DEFAULT (now()),
                    work_date date NOT NULL,
                    CONSTRAINT employee_shifts_pkey PRIMARY KEY (id),
                    CONSTRAINT employee_shifts_employee_id_fkey FOREIGN KEY (employee_id) REFERENCES smart_pool.employees (user_id),
                    CONSTRAINT employee_shifts_shift_id_fkey FOREIGN KEY (shift_id) REFERENCES smart_pool.shifts (id)
                );

                CREATE TABLE smart_pool.orders (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    employee_id uuid,
                    user_id uuid,
                    voucher_id uuid,
                    created_at timestamp with time zone DEFAULT (now()),
                    customer_name character varying(255),
                    customer_phone character varying(20),
                    discount_amount numeric(18,2) DEFAULT (0),
                    final_amount numeric(18,2) NOT NULL,
                    is_deleted boolean DEFAULT FALSE,
                    status character varying(50) DEFAULT ('Pending'::character varying),
                    total_amount numeric(18,2) NOT NULL,
                    updated_at timestamp with time zone DEFAULT (now()),
                    CONSTRAINT orders_pkey PRIMARY KEY (id),
                    CONSTRAINT orders_employee_id_fkey FOREIGN KEY (employee_id) REFERENCES smart_pool.employees (user_id),
                    CONSTRAINT orders_user_id_fkey FOREIGN KEY (user_id) REFERENCES smart_pool.users (id),
                    CONSTRAINT orders_voucher_id_fkey FOREIGN KEY (voucher_id) REFERENCES smart_pool.vouchers (id)
                );

                CREATE TABLE smart_pool.salaries (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    employee_id uuid NOT NULL,
                    bonus numeric(18,2) DEFAULT (0),
                    created_at timestamp with time zone DEFAULT (now()),
                    deduction numeric(18,2) DEFAULT (0),
                    month integer NOT NULL,
                    net_salary numeric(18,2) NOT NULL,
                    total_shifts integer DEFAULT 0,
                    year integer NOT NULL,
                    CONSTRAINT salaries_pkey PRIMARY KEY (id),
                    CONSTRAINT salaries_employee_id_fkey FOREIGN KEY (employee_id) REFERENCES smart_pool.employees (user_id)
                );

                CREATE TABLE smart_pool.maintenance_logs (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    equipment_id uuid NOT NULL,
                    performed_by uuid,
                    schedule_id uuid,
                    actual_date timestamp with time zone NOT NULL,
                    cost numeric(18,2) DEFAULT (0),
                    created_at timestamp with time zone DEFAULT (now()),
                    note text,
                    vendor_name character varying(255),
                    CONSTRAINT maintenance_logs_pkey PRIMARY KEY (id),
                    CONSTRAINT maintenance_logs_equipment_id_fkey FOREIGN KEY (equipment_id) REFERENCES smart_pool.equipments (id),
                    CONSTRAINT maintenance_logs_performed_by_fkey FOREIGN KEY (performed_by) REFERENCES smart_pool.users (id),
                    CONSTRAINT maintenance_logs_schedule_id_fkey FOREIGN KEY (schedule_id) REFERENCES smart_pool.maintenance_schedules (id)
                );

                CREATE TABLE smart_pool.entry_logs (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    operator_id uuid,
                    ticket_id uuid,
                    gate_id character varying(50),
                    input_mode character varying(50),
                    message text,
                    scan_time timestamp with time zone DEFAULT (now()),
                    status character varying(50) NOT NULL,
                    CONSTRAINT entry_logs_pkey PRIMARY KEY (id),
                    CONSTRAINT entry_logs_operator_id_fkey FOREIGN KEY (operator_id) REFERENCES smart_pool.users (id),
                    CONSTRAINT entry_logs_ticket_id_fkey FOREIGN KEY (ticket_id) REFERENCES smart_pool.tickets (id)
                );

                CREATE TABLE smart_pool.order_details (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    order_id uuid NOT NULL,
                    created_at timestamp with time zone DEFAULT (now()),
                    item_id uuid NOT NULL,
                    item_type character varying(50) NOT NULL,
                    quantity integer NOT NULL,
                    unit_price numeric(18,2) NOT NULL,
                    CONSTRAINT order_details_pkey PRIMARY KEY (id),
                    CONSTRAINT order_details_order_id_fkey FOREIGN KEY (order_id) REFERENCES smart_pool.orders (id)
                );

                CREATE TABLE smart_pool.payments (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    cashier_id uuid,
                    order_id uuid NOT NULL,
                    amount numeric(18,2) NOT NULL,
                    created_at timestamp with time zone DEFAULT (now()),
                    payment_method character varying(50) NOT NULL,
                    payment_time timestamp with time zone DEFAULT (now()),
                    status character varying(50) DEFAULT ('Completed'::character varying),
                    transaction_ref character varying(255),
                    CONSTRAINT payments_pkey PRIMARY KEY (id),
                    CONSTRAINT payments_cashier_id_fkey FOREIGN KEY (cashier_id) REFERENCES smart_pool.employees (user_id),
                    CONSTRAINT payments_order_id_fkey FOREIGN KEY (order_id) REFERENCES smart_pool.orders (id)
                );

                CREATE TABLE smart_pool.rentals (
                    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
                    order_id uuid NOT NULL,
                    product_id uuid NOT NULL,
                    deposit_amount numeric(18,2) DEFAULT (0),
                    rent_time timestamp with time zone DEFAULT (now()),
                    return_time timestamp with time zone,
                    status character varying(50) DEFAULT ('Renting'::character varying),
                    updated_at timestamp with time zone DEFAULT (now()),
                    CONSTRAINT rentals_pkey PRIMARY KEY (id),
                    CONSTRAINT rentals_order_id_fkey FOREIGN KEY (order_id) REFERENCES smart_pool.orders (id),
                    CONSTRAINT rentals_product_id_fkey FOREIGN KEY (product_id) REFERENCES smart_pool.products (id)
                );

                CREATE INDEX "IX_employee_shifts_shift_id" ON smart_pool.employee_shifts (shift_id);
                CREATE UNIQUE INDEX ux_employee_shifts_employee_id_shift_id_work_date ON smart_pool.employee_shifts (employee_id, shift_id, work_date);
                CREATE INDEX "IX_entry_logs_operator_id" ON smart_pool.entry_logs (operator_id);
                CREATE INDEX ix_entry_logs_scan_time ON smart_pool.entry_logs (scan_time);
                CREATE INDEX ix_entry_logs_ticket_id_scan_time ON smart_pool.entry_logs (ticket_id, scan_time);
                CREATE INDEX "IX_incidents_reported_by" ON smart_pool.incidents (reported_by);
                CREATE INDEX "IX_inventory_logs_created_by" ON smart_pool.inventory_logs (created_by);
                CREATE INDEX "IX_inventory_logs_product_id" ON smart_pool.inventory_logs (product_id);
                CREATE INDEX "IX_maintenance_logs_equipment_id" ON smart_pool.maintenance_logs (equipment_id);
                CREATE INDEX "IX_maintenance_logs_performed_by" ON smart_pool.maintenance_logs (performed_by);
                CREATE INDEX "IX_maintenance_logs_schedule_id" ON smart_pool.maintenance_logs (schedule_id);
                CREATE INDEX "IX_maintenance_schedules_assigned_to" ON smart_pool.maintenance_schedules (assigned_to);
                CREATE INDEX "IX_maintenance_schedules_equipment_id" ON smart_pool.maintenance_schedules (equipment_id);
                CREATE INDEX "IX_notifications_user_id" ON smart_pool.notifications (user_id);
                CREATE INDEX "IX_order_details_order_id" ON smart_pool.order_details (order_id);
                CREATE INDEX "IX_orders_employee_id" ON smart_pool.orders (employee_id);
                CREATE INDEX "IX_orders_user_id" ON smart_pool.orders (user_id);
                CREATE INDEX "IX_orders_voucher_id" ON smart_pool.orders (voucher_id);
                CREATE INDEX "IX_payments_cashier_id" ON smart_pool.payments (cashier_id);
                CREATE INDEX "IX_payments_order_id" ON smart_pool.payments (order_id);
                CREATE INDEX "IX_rentals_order_id" ON smart_pool.rentals (order_id);
                CREATE INDEX "IX_rentals_product_id" ON smart_pool.rentals (product_id);
                CREATE UNIQUE INDEX ux_salaries_employee_id_month_year ON smart_pool.salaries (employee_id, month, year);
                CREATE INDEX "IX_tickets_ticket_type_id" ON smart_pool.tickets (ticket_type_id);
                CREATE INDEX "IX_tickets_user_id" ON smart_pool.tickets (user_id);
                CREATE UNIQUE INDEX tickets_qr_code_key ON smart_pool.tickets (qr_code);
                CREATE INDEX "IX_users_role_id" ON smart_pool.users (role_id);
                CREATE UNIQUE INDEX users_email_key ON smart_pool.users (email);
                CREATE UNIQUE INDEX users_phone_key ON smart_pool.users (phone);
                CREATE UNIQUE INDEX users_username_key ON smart_pool.users (username);
                CREATE UNIQUE INDEX vouchers_code_key ON smart_pool.vouchers (code);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_shifts",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "entry_logs",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "incidents",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "inventory_logs",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "maintenance_logs",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "order_details",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "rentals",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "salaries",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "user_profiles",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "shifts",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "tickets",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "maintenance_schedules",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "products",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "ticket_types",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "equipments",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "employees",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "vouchers",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "users",
                schema: "smart_pool");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "smart_pool");
        }
    }
}
