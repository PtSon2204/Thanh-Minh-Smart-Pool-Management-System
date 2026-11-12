-- ============================================================================
-- SCRIPT TẠO DỮ LIỆU MẪU CHO TẤT CẢ CÁC MỤC THỐNG KÊ (POSTGRESQL)
-- Schema: smart_pool
-- ============================================================================

DO $$
DECLARE
    v_role_customer_id UUID;
    v_role_staff_id UUID;
    v_user1_id UUID;
    v_user2_id UUID;
    v_user3_id UUID;
    v_user4_id UUID;
    v_user5_id UUID;
    v_staff_id UUID;

    v_tt_single_adult UUID;
    v_tt_single_child UUID;
    v_tt_month_adult  UUID;
    v_tt_month_child  UUID;

    v_prod_kinh UUID;
    v_prod_phao UUID;
    v_prod_non  UUID;
    v_prod_quan UUID;

    v_order_id UUID;
    v_ticket_id UUID;
    v_rental_id UUID;
    v_now TIMESTAMP WITH TIME ZONE := NOW();
BEGIN
    -- 1. Đảm bảo Role tồn tại
    SELECT id INTO v_role_customer_id FROM smart_pool.roles WHERE UPPER(name) = 'CUSTOMER' OR UPPER(name) = 'MEMBER' LIMIT 1;
    IF v_role_customer_id IS NULL THEN
        v_role_customer_id := gen_random_uuid();
        INSERT INTO smart_pool.roles (id, name, permissions, is_deleted, created_at, updated_at)
        VALUES (v_role_customer_id, 'CUSTOMER', '[]'::jsonb, false, v_now, v_now);
    END IF;

    SELECT id INTO v_role_staff_id FROM smart_pool.roles WHERE UPPER(name) IN ('ADMIN', 'STAFF', 'LIFEGUARD') LIMIT 1;
    IF v_role_staff_id IS NULL THEN
        v_role_staff_id := v_role_customer_id;
    END IF;

    -- 2. Đảm bảo Nhân viên / Thu ngân tồn tại
    SELECT id INTO v_staff_id FROM smart_pool.users WHERE role_id = v_role_staff_id LIMIT 1;
    IF v_staff_id IS NULL THEN
        v_staff_id := gen_random_uuid();
        INSERT INTO smart_pool.users (id, username, email, phone, role_id, status, is_deleted, created_at, updated_at)
        VALUES (v_staff_id, 'staff_demo', 'staff_demo@smartpool.vn', '0900111222', v_role_staff_id, 'Active', false, v_now, v_now);
        INSERT INTO smart_pool.user_profiles (user_id, full_name, updated_at)
        VALUES (v_staff_id, 'Nhân Viên Thu Ngân', v_now);
    END IF;

    -- 3. Tạo 5 Khách hàng thực tế
    v_user1_id := gen_random_uuid();
    v_user2_id := gen_random_uuid();
    v_user3_id := gen_random_uuid();
    v_user4_id := gen_random_uuid();
    v_user5_id := gen_random_uuid();

    INSERT INTO smart_pool.users (id, username, email, phone, role_id, status, is_deleted, created_at, updated_at)
    VALUES
        (v_user1_id, 'nguyen.van.an', 'an.nguyen@gmail.com', '0901234567', v_role_customer_id, 'Active', false, v_now - INTERVAL '20 days', v_now),
        (v_user2_id, 'tran.thi.mai', 'mai.tran@gmail.com', '0912345678', v_role_customer_id, 'Active', false, v_now - INTERVAL '15 days', v_now),
        (v_user3_id, 'le.hoang.nam', 'nam.le@gmail.com', '0923456789', v_role_customer_id, 'Active', false, v_now - INTERVAL '10 days', v_now),
        (v_user4_id, 'pham.thanh.huong', 'huong.pham@gmail.com', '0934567890', v_role_customer_id, 'Active', false, v_now - INTERVAL '5 days', v_now),
        (v_user5_id, 'do.minh.tuan', 'tuan.do@gmail.com', '0945678901', v_role_customer_id, 'Active', false, v_now - INTERVAL '2 days', v_now)
    ON CONFLICT (email) DO NOTHING;

    -- Nếu bị trùng email thì lấy lại ID đã có
    SELECT id INTO v_user1_id FROM smart_pool.users WHERE email = 'an.nguyen@gmail.com';
    SELECT id INTO v_user2_id FROM smart_pool.users WHERE email = 'mai.tran@gmail.com';
    SELECT id INTO v_user3_id FROM smart_pool.users WHERE email = 'nam.le@gmail.com';
    SELECT id INTO v_user4_id FROM smart_pool.users WHERE email = 'huong.pham@gmail.com';
    SELECT id INTO v_user5_id FROM smart_pool.users WHERE email = 'tuan.do@gmail.com';

    INSERT INTO smart_pool.user_profiles (user_id, full_name, updated_at)
    VALUES
        (v_user1_id, 'Nguyễn Văn An', v_now),
        (v_user2_id, 'Trần Thị Mai', v_now),
        (v_user3_id, 'Lê Hoàng Nam', v_now),
        (v_user4_id, 'Phạm Thanh Hương', v_now),
        (v_user5_id, 'Đỗ Minh Tuấn', v_now)
    ON CONFLICT (user_id) DO UPDATE SET full_name = EXCLUDED.full_name;

    -- 4. Tạo hoặc lấy Loại vé (Ticket Types)
    SELECT id INTO v_tt_single_adult FROM smart_pool.ticket_types WHERE ticket_category = 'VE_LUOT' AND name ILIKE '%Người Lớn%' LIMIT 1;
    IF v_tt_single_adult IS NULL THEN
        v_tt_single_adult := gen_random_uuid();
        INSERT INTO smart_pool.ticket_types (id, name, price, duration_days, total_entries, ticket_category, is_active, is_deleted, created_at, updated_at)
        VALUES (v_tt_single_adult, 'Vé Lượt Người Lớn', 50000, 1, 1, 'VE_LUOT', true, false, v_now - INTERVAL '30 days', v_now);
    END IF;

    SELECT id INTO v_tt_single_child FROM smart_pool.ticket_types WHERE ticket_category = 'VE_LUOT' AND name ILIKE '%Trẻ Em%' LIMIT 1;
    IF v_tt_single_child IS NULL THEN
        v_tt_single_child := gen_random_uuid();
        INSERT INTO smart_pool.ticket_types (id, name, price, duration_days, total_entries, ticket_category, is_active, is_deleted, created_at, updated_at)
        VALUES (v_tt_single_child, 'Vé Lượt Trẻ Em', 35000, 1, 1, 'VE_LUOT', true, false, v_now - INTERVAL '30 days', v_now);
    END IF;

    SELECT id INTO v_tt_month_adult FROM smart_pool.ticket_types WHERE ticket_category = 'VE_THANG' AND name ILIKE '%Người Lớn%' LIMIT 1;
    IF v_tt_month_adult IS NULL THEN
        v_tt_month_adult := gen_random_uuid();
        INSERT INTO smart_pool.ticket_types (id, name, price, duration_days, total_entries, ticket_category, is_active, is_deleted, created_at, updated_at)
        VALUES (v_tt_month_adult, 'Vé Tháng Người Lớn', 500000, 30, 30, 'VE_THANG', true, false, v_now - INTERVAL '30 days', v_now);
    END IF;

    SELECT id INTO v_tt_month_child FROM smart_pool.ticket_types WHERE ticket_category = 'VE_THANG' AND name ILIKE '%Trẻ Em%' LIMIT 1;
    IF v_tt_month_child IS NULL THEN
        v_tt_month_child := gen_random_uuid();
        INSERT INTO smart_pool.ticket_types (id, name, price, duration_days, total_entries, ticket_category, is_active, is_deleted, created_at, updated_at)
        VALUES (v_tt_month_child, 'Vé Tháng Trẻ Em', 350000, 30, 30, 'VE_THANG', true, false, v_now - INTERVAL '30 days', v_now);
    END IF;

    -- 5. Tạo Sản phẩm cho thuê (Products)
    SELECT id INTO v_prod_kinh FROM smart_pool.products WHERE name = 'Kính bơi cao cấp' LIMIT 1;
    IF v_prod_kinh IS NULL THEN
        v_prod_kinh := gen_random_uuid();
        INSERT INTO smart_pool.products (id, name, price, stock_quantity, is_active, is_deleted, created_at, updated_at)
        VALUES (v_prod_kinh, 'Kính bơi cao cấp', 15000, 50, true, false, v_now, v_now);
    END IF;

    SELECT id INTO v_prod_phao FROM smart_pool.products WHERE name = 'Phao bơi tay' LIMIT 1;
    IF v_prod_phao IS NULL THEN
        v_prod_phao := gen_random_uuid();
        INSERT INTO smart_pool.products (id, name, price, stock_quantity, is_active, is_deleted, created_at, updated_at)
        VALUES (v_prod_phao, 'Phao bơi tay', 20000, 40, true, false, v_now, v_now);
    END IF;

    SELECT id INTO v_prod_non FROM smart_pool.products WHERE name = 'Mũ bơi silicon' LIMIT 1;
    IF v_prod_non IS NULL THEN
        v_prod_non := gen_random_uuid();
        INSERT INTO smart_pool.products (id, name, price, stock_quantity, is_active, is_deleted, created_at, updated_at)
        VALUES (v_prod_non, 'Mũ bơi silicon', 10000, 60, true, false, v_now, v_now);
    END IF;

    -- 6. Tạo đơn hàng bán vé và vé cho 5 ngày gần đây
    -- =========================================================================
    -- Đơn 1: 5 ngày trước - Vé tháng người lớn
    v_order_id := gen_random_uuid();
    INSERT INTO smart_pool.orders (id, user_id, customer_name, customer_phone, total_amount, discount_amount, final_amount, status, is_deleted, created_at, updated_at)
    VALUES (v_order_id, v_user1_id, 'Nguyễn Văn An', '0901234567', 500000, 0, 500000, 'Completed', false, v_now - INTERVAL '5 days', v_now - INTERVAL '5 days');
    
    INSERT INTO smart_pool.order_details (id, order_id, item_id, item_type, unit_price, quantity, created_at)
    VALUES (gen_random_uuid(), v_order_id, v_tt_month_adult, 'TICKET', 500000, 1, v_now - INTERVAL '5 days');

    INSERT INTO smart_pool.payments (id, order_id, cashier_id, amount, payment_method, status, transaction_ref, payment_time, created_at)
    VALUES (gen_random_uuid(), v_order_id, v_staff_id, 500000, 'Chuyển khoản', 'Completed', 'TXN_MONTH_01', v_now - INTERVAL '5 days', v_now - INTERVAL '5 days');

    v_ticket_id := gen_random_uuid();
    INSERT INTO smart_pool.tickets (id, user_id, ticket_type_id, qr_code, issue_date, expiry_date, remaining_entries, status, is_deleted, created_at, updated_at)
    VALUES (v_ticket_id, v_user1_id, v_tt_month_adult, 'QR_M01_' || SUBSTRING(v_ticket_id::text, 1, 8), v_now - INTERVAL '5 days', v_now + INTERVAL '25 days', 28, 'Active', false, v_now - INTERVAL '5 days', v_now - INTERVAL '5 days');

    -- Quẹt thẻ vào cổng cho vé tháng này
    INSERT INTO smart_pool.entry_logs (id, ticket_id, operator_id, scan_time, gate_id, input_mode, status, message)
    VALUES
        (gen_random_uuid(), v_ticket_id, v_staff_id, v_now - INTERVAL '5 days' + INTERVAL '7 hours', 'Cổng 1', 'QR', 'VALID', 'Hợp lệ'),
        (gen_random_uuid(), v_ticket_id, v_staff_id, v_now - INTERVAL '3 days' + INTERVAL '17 hours 30 min', 'Cổng 1', 'QR', 'VALID', 'Hợp lệ'),
        (gen_random_uuid(), v_ticket_id, v_staff_id, v_now - INTERVAL '1 days' + INTERVAL '6 hours 45 min', 'Cổng 1', 'QR', 'VALID', 'Hợp lệ');

    -- Đơn 2: 4 ngày trước - 2 Vé lượt người lớn
    v_order_id := gen_random_uuid();
    INSERT INTO smart_pool.orders (id, user_id, customer_name, customer_phone, total_amount, discount_amount, final_amount, status, is_deleted, created_at, updated_at)
    VALUES (v_order_id, v_user2_id, 'Trần Thị Mai', '0912345678', 100000, 0, 100000, 'Completed', false, v_now - INTERVAL '4 days', v_now - INTERVAL '4 days');
    
    INSERT INTO smart_pool.order_details (id, order_id, item_id, item_type, unit_price, quantity, created_at)
    VALUES (gen_random_uuid(), v_order_id, v_tt_single_adult, 'TICKET', 50000, 2, v_now - INTERVAL '4 days');

    INSERT INTO smart_pool.payments (id, order_id, cashier_id, amount, payment_method, status, transaction_ref, payment_time, created_at)
    VALUES (gen_random_uuid(), v_order_id, v_staff_id, 100000, 'Tiền mặt', 'Completed', 'TXN_SINGLE_02', v_now - INTERVAL '4 days', v_now - INTERVAL '4 days');

    v_ticket_id := gen_random_uuid();
    INSERT INTO smart_pool.tickets (id, user_id, ticket_type_id, qr_code, issue_date, expiry_date, remaining_entries, status, is_deleted, created_at, updated_at)
    VALUES (v_ticket_id, v_user2_id, v_tt_single_adult, 'QR_S02_' || SUBSTRING(v_ticket_id::text, 1, 8), v_now - INTERVAL '4 days', v_now - INTERVAL '4 days' + INTERVAL '1 day', 0, 'Used', false, v_now - INTERVAL '4 days', v_now - INTERVAL '4 days');

    INSERT INTO smart_pool.entry_logs (id, ticket_id, operator_id, scan_time, gate_id, input_mode, status, message)
    VALUES (gen_random_uuid(), v_ticket_id, v_staff_id, v_now - INTERVAL '4 days' + INTERVAL '16 hours', 'Cổng 1', 'QR', 'VALID', 'Đã check-in');

    -- Đơn 3: 3 ngày trước - 1 Vé tháng trẻ em
    v_order_id := gen_random_uuid();
    INSERT INTO smart_pool.orders (id, user_id, customer_name, customer_phone, total_amount, discount_amount, final_amount, status, is_deleted, created_at, updated_at)
    VALUES (v_order_id, v_user3_id, 'Lê Hoàng Nam', '0923456789', 350000, 0, 350000, 'Completed', false, v_now - INTERVAL '3 days', v_now - INTERVAL '3 days');

    INSERT INTO smart_pool.order_details (id, order_id, item_id, item_type, unit_price, quantity, created_at)
    VALUES (gen_random_uuid(), v_order_id, v_tt_month_child, 'TICKET', 350000, 1, v_now - INTERVAL '3 days');

    INSERT INTO smart_pool.payments (id, order_id, cashier_id, amount, payment_method, status, transaction_ref, payment_time, created_at)
    VALUES (gen_random_uuid(), v_order_id, v_staff_id, 350000, 'Chuyển khoản', 'Completed', 'TXN_MONTH_03', v_now - INTERVAL '3 days', v_now - INTERVAL '3 days');

    v_ticket_id := gen_random_uuid();
    INSERT INTO smart_pool.tickets (id, user_id, ticket_type_id, qr_code, issue_date, expiry_date, remaining_entries, status, is_deleted, created_at, updated_at)
    VALUES (v_ticket_id, v_user3_id, v_tt_month_child, 'QR_MC03_' || SUBSTRING(v_ticket_id::text, 1, 8), v_now - INTERVAL '3 days', v_now + INTERVAL '27 days', 29, 'Active', false, v_now - INTERVAL '3 days', v_now - INTERVAL '3 days');

    INSERT INTO smart_pool.entry_logs (id, ticket_id, operator_id, scan_time, gate_id, input_mode, status, message)
    VALUES (gen_random_uuid(), v_ticket_id, v_staff_id, v_now - INTERVAL '3 days' + INTERVAL '18 hours', 'Cổng 2', 'QR', 'VALID', 'Hợp lệ');

    -- Đơn 4: Hôm qua - 3 Vé lượt trẻ em + 1 Vé lượt người lớn
    v_order_id := gen_random_uuid();
    INSERT INTO smart_pool.orders (id, user_id, customer_name, customer_phone, total_amount, discount_amount, final_amount, status, is_deleted, created_at, updated_at)
    VALUES (v_order_id, v_user4_id, 'Phạm Thanh Hương', '0934567890', 155000, 0, 155000, 'Completed', false, v_now - INTERVAL '1 days', v_now - INTERVAL '1 days');

    INSERT INTO smart_pool.order_details (id, order_id, item_id, item_type, unit_price, quantity, created_at)
    VALUES
        (gen_random_uuid(), v_order_id, v_tt_single_child, 'TICKET', 35000, 3, v_now - INTERVAL '1 days'),
        (gen_random_uuid(), v_order_id, v_tt_single_adult, 'TICKET', 50000, 1, v_now - INTERVAL '1 days');

    INSERT INTO smart_pool.payments (id, order_id, cashier_id, amount, payment_method, status, transaction_ref, payment_time, created_at)
    VALUES (gen_random_uuid(), v_order_id, v_staff_id, 155000, 'Tiền mặt', 'Completed', 'TXN_SINGLE_04', v_now - INTERVAL '1 days', v_now - INTERVAL '1 days');

    v_ticket_id := gen_random_uuid();
    INSERT INTO smart_pool.tickets (id, user_id, ticket_type_id, qr_code, issue_date, expiry_date, remaining_entries, status, is_deleted, created_at, updated_at)
    VALUES (v_ticket_id, v_user4_id, v_tt_single_adult, 'QR_S04_' || SUBSTRING(v_ticket_id::text, 1, 8), v_now - INTERVAL '1 days', v_now, 0, 'Used', false, v_now - INTERVAL '1 days', v_now - INTERVAL '1 days');

    INSERT INTO smart_pool.entry_logs (id, ticket_id, operator_id, scan_time, gate_id, input_mode, status, message)
    VALUES (gen_random_uuid(), v_ticket_id, v_staff_id, v_now - INTERVAL '1 days' + INTERVAL '17 hours 10 min', 'Cổng 1', 'QR', 'VALID', 'Đã check-in');

    -- Đơn 5: Hôm nay - 2 Vé lượt người lớn
    v_order_id := gen_random_uuid();
    INSERT INTO smart_pool.orders (id, user_id, customer_name, customer_phone, total_amount, discount_amount, final_amount, status, is_deleted, created_at, updated_at)
    VALUES (v_order_id, v_user5_id, 'Đỗ Minh Tuấn', '0945678901', 100000, 0, 100000, 'Completed', false, v_now - INTERVAL '2 hours', v_now);

    INSERT INTO smart_pool.order_details (id, order_id, item_id, item_type, unit_price, quantity, created_at)
    VALUES (gen_random_uuid(), v_order_id, v_tt_single_adult, 'TICKET', 50000, 2, v_now - INTERVAL '2 hours');

    INSERT INTO smart_pool.payments (id, order_id, cashier_id, amount, payment_method, status, transaction_ref, payment_time, created_at)
    VALUES (gen_random_uuid(), v_order_id, v_staff_id, 100000, 'Tiền mặt', 'Completed', 'TXN_SINGLE_05', v_now - INTERVAL '2 hours', v_now);

    v_ticket_id := gen_random_uuid();
    INSERT INTO smart_pool.tickets (id, user_id, ticket_type_id, qr_code, issue_date, expiry_date, remaining_entries, status, is_deleted, created_at, updated_at)
    VALUES (v_ticket_id, v_user5_id, v_tt_single_adult, 'QR_S05_' || SUBSTRING(v_ticket_id::text, 1, 8), v_now - INTERVAL '2 hours', v_now + INTERVAL '1 day', 1, 'Active', false, v_now - INTERVAL '2 hours', v_now);

    INSERT INTO smart_pool.entry_logs (id, ticket_id, operator_id, scan_time, gate_id, input_mode, status, message)
    VALUES (gen_random_uuid(), v_ticket_id, v_staff_id, v_now - INTERVAL '1 hour', 'Cổng 1', 'QR', 'VALID', 'Đã check-in');

    -- 7. Tạo Dữ liệu cho Thuê đồ (Rentals & Rental Transactions)
    -- =========================================================================
    -- Thuê đồ 1: Đã hoàn thành, trả đồ bình thường (3 ngày trước)
    v_rental_id := gen_random_uuid();
    INSERT INTO smart_pool.rentals (id, order_id, product_id, rent_time, return_time, deposit_amount, fee_amount, product_name_snapshot, status, updated_at)
    VALUES (v_rental_id, v_order_id, v_prod_kinh, v_now - INTERVAL '3 days', v_now - INTERVAL '3 days' + INTERVAL '2 hours', 20000, 15000, 'Kính bơi cao cấp', 'Returned', v_now - INTERVAL '3 days');

    -- Thuê đồ 2: Đã hoàn thành, trả đồ bình thường (Hôm qua)
    v_rental_id := gen_random_uuid();
    INSERT INTO smart_pool.rentals (id, order_id, product_id, rent_time, return_time, deposit_amount, fee_amount, product_name_snapshot, status, updated_at)
    VALUES (v_rental_id, v_order_id, v_prod_phao, v_now - INTERVAL '1 days', v_now - INTERVAL '1 days' + INTERVAL '1 hour 30 min', 20000, 20000, 'Phao bơi tay', 'Returned', v_now - INTERVAL '1 days');

    -- Thuê đồ 3: Đang thuê (Renting) trong hôm nay
    v_rental_id := gen_random_uuid();
    INSERT INTO smart_pool.rentals (id, order_id, product_id, rent_time, return_time, deposit_amount, fee_amount, product_name_snapshot, status, updated_at)
    VALUES (v_rental_id, v_order_id, v_prod_non, v_now - INTERVAL '3 hours', NULL, 20000, 10000, 'Mũ bơi silicon', 'Renting', v_now);

    -- Thuê đồ 4: Bị hỏng/mất (Damaged) - Khấu trừ cọc 20,000 VND
    v_rental_id := gen_random_uuid();
    INSERT INTO smart_pool.rentals (id, order_id, product_id, rent_time, return_time, deposit_amount, fee_amount, product_name_snapshot, status, updated_at)
    VALUES (v_rental_id, v_order_id, v_prod_kinh, v_now - INTERVAL '2 days', v_now - INTERVAL '2 days' + INTERVAL '1 hour', 20000, 15000, 'Kính bơi cao cấp', 'Damaged', v_now - INTERVAL '2 days');

    INSERT INTO smart_pool.rental_transactions (id, order_id, product_id, payment_id, kind, request_hash, fee_amount, deposit_amount, refund_amount, retained_amount, refund_method, confirmed_by_user_id, confirmed_at_utc, reason, result_json)
    VALUES (gen_random_uuid(), v_order_id, v_prod_kinh, NULL, 'Settlement', 'HASH_SETTLE_01', 0, 20000, 0, 20000, NULL, v_staff_id, v_now - INTERVAL '2 days', 'Khách làm gãy gọng kính bơi', '{"status":"Damaged","retained":20000}');

    -- 8. Tạo Dữ liệu Sự cố (Incidents)
    -- =========================================================================
    INSERT INTO smart_pool.incidents (id, title, description, severity_level, status, reported_by, incident_time, location, created_at, updated_at)
    VALUES
        (gen_random_uuid(), 'Áp suất máy bơm lọc số 2 giảm bất thường', 'Hệ thống bơm lọc tuần hoàn bể lớn phát hiện tụt áp suất nước', 'HIGH', 'RESOLVED', v_staff_id, v_now - INTERVAL '4 days' + INTERVAL '8 hours', 'Phòng kỹ thuật ngầm B1', v_now - INTERVAL '4 days', v_now - INTERVAL '4 days' + INTERVAL '2 hours'),
        (gen_random_uuid(), 'Khách hàng có dấu hiệu chuột rút tại làn 3', 'Cứu hộ đã kịp thời quăng phao và đưa khách lên bờ sơ cứu an toàn', 'MEDIUM', 'RESOLVED', v_staff_id, v_now - INTERVAL '2 days' + INTERVAL '17 hours', 'Bể bơi người lớn làn 3', v_now - INTERVAL '2 days', v_now - INTERVAL '2 days' + INTERVAL '30 min'),
        (gen_random_uuid(), 'Nồng độ Clo dư vượt ngưỡng nhẹ 1.8 mg/L', 'Đo chất lượng nước đầu ca chiều phát hiện Clo dư cao hơn quy chuẩn, đã giảm định lượng bơm hóa chất', 'LOW', 'RESOLVED', v_staff_id, v_now - INTERVAL '1 days' + INTERVAL '14 hours', 'Bể thiếu nhi', v_now - INTERVAL '1 days', v_now - INTERVAL '1 days' + INTERVAL '1 hour'),
        (gen_random_uuid(), 'Đèn chiếu sáng dưới nước góc trái nhấp nháy', 'Đèn LED âm nước 12V tại góc phía đông có hiện tượng chớp tắt, đang kiểm tra biến áp', 'LOW', 'IN_PROGRESS', v_staff_id, v_now - INTERVAL '5 hours', 'Góc đông bể người lớn', v_now - INTERVAL '5 hours', v_now - INTERVAL '1 hour');

    RAISE NOTICE 'Đã chèn dữ liệu mẫu thành công vào schema smart_pool!';
END $$;

