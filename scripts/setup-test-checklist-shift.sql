-- ==============================================================================
-- Script thiết lập dữ liệu test Checklist Thiết Bị & Phân Ca Hôm Nay (19/11/2026)
-- CSDL: PostgreSQL (ThanhMinhPoolDB) - Schema: smart_pool
-- Mật khẩu chung cho tất cả tài khoản cứu hộ: Password123!
-- ==============================================================================

BEGIN;

-- 1. Cập nhật giờ kết thúc của Ca chiều đến 23:30 để khớp với FixedShifts.Afternoon trong C#
-- (Cho phép mở kiểm tra thiết bị đến đêm nay mà không bị hết giờ)
UPDATE smart_pool.shifts
SET end_time = '23:30:00'
WHERE id = '1a6d3c2b-8e4f-4b7c-9d0e-2f3a4b5c6d7e';

-- 2. Tạo 2 tài khoản Cứu hộ mới: lifeguard1 & lifeguard2 (mật khẩu: Password123!)
-- Hash BCrypt chuẩn của 'Password123!': $2a$11$Ys/FVDm.XX14IRtgTzG90OmtSaovptXGjMYGpCEOyfpjLAZvdTEEu
INSERT INTO smart_pool.users (id, username, email, phone, password_hash, role_id, status, is_coach, is_deleted, created_at, updated_at)
VALUES 
  ('2a222222-2222-2222-2222-222222222222', 'lifeguard1', 'lifeguard1@smartpool.vn', '0901234561', '$2a$11$Ys/FVDm.XX14IRtgTzG90OmtSaovptXGjMYGpCEOyfpjLAZvdTEEu', '1efc6d84-ca33-4e8e-a9a1-f56240dd77a4', 'Active', false, false, NOW(), NOW()),
  ('3a333333-3333-3333-3333-333333333333', 'lifeguard2', 'lifeguard2@smartpool.vn', '0901234562', '$2a$11$Ys/FVDm.XX14IRtgTzG90OmtSaovptXGjMYGpCEOyfpjLAZvdTEEu', '1efc6d84-ca33-4e8e-a9a1-f56240dd77a4', 'Active', false, false, NOW(), NOW())
ON CONFLICT (id) DO UPDATE SET 
  username = EXCLUDED.username,
  email = EXCLUDED.email,
  phone = EXCLUDED.phone,
  password_hash = EXCLUDED.password_hash,
  role_id = EXCLUDED.role_id,
  status = EXCLUDED.status,
  is_deleted = false;

INSERT INTO smart_pool.user_profiles (user_id, full_name, updated_at)
VALUES 
  ('2a222222-2222-2222-2222-222222222222', 'Nguyễn Văn Cứu Hộ 1', NOW()),
  ('3a333333-3333-3333-3333-333333333333', 'Trần Thị Cứu Hộ 2', NOW())
ON CONFLICT (user_id) DO UPDATE SET 
  full_name = EXCLUDED.full_name;

INSERT INTO smart_pool.employees (user_id, status, join_date, created_at, updated_at)
VALUES 
  ('2a222222-2222-2222-2222-222222222222', 'Working', '2024-03-01', NOW(), NOW()),
  ('3a333333-3333-3333-3333-333333333333', 'Working', '2024-03-01', NOW(), NOW())
ON CONFLICT (user_id) DO UPDATE SET 
  status = EXCLUDED.status;

-- Cập nhật mật khẩu cho cả lifeguard_staff hiện có thành 'Password123!' để thuận tiện test
UPDATE smart_pool.users 
SET password_hash = '$2a$11$Ys/FVDm.XX14IRtgTzG90OmtSaovptXGjMYGpCEOyfpjLAZvdTEEu'
WHERE id = 'e35662a5-1d14-4c22-a2ba-c7158b91e8b2';

-- 3. Gán ca làm việc hôm nay 19/11/2026 cho 2 cứu hộ vào Ca chiều (13:30 - 23:30)
INSERT INTO smart_pool.employee_shifts (id, employee_id, shift_id, work_date, status, created_at, updated_at)
VALUES 
  ('e1111111-1111-1111-1111-111111111111', '2a222222-2222-2222-2222-222222222222', '1a6d3c2b-8e4f-4b7c-9d0e-2f3a4b5c6d7e', '2026-11-19', 'Scheduled', NOW(), NOW()),
  ('e2222222-2222-2222-2222-222222222222', '3a333333-3333-3333-3333-333333333333', '1a6d3c2b-8e4f-4b7c-9d0e-2f3a4b5c6d7e', '2026-11-19', 'Scheduled', NOW(), NOW())
ON CONFLICT (id) DO UPDATE SET 
  employee_id = EXCLUDED.employee_id,
  shift_id = EXCLUDED.shift_id,
  work_date = EXCLUDED.work_date,
  status = EXCLUDED.status;

-- 4. Gán nhiệm vụ checklist thiết bị Ca chiều hôm nay cho lifeguard1
UPDATE smart_pool.pool_duty_schedules
SET 
  assigned_employee_id = '2a222222-2222-2222-2222-222222222222',
  preferred_employee_id = '2a222222-2222-2222-2222-222222222222',
  shift_id = '1a6d3c2b-8e4f-4b7c-9d0e-2f3a4b5c6d7e',
  employee_shift_id = 'e1111111-1111-1111-1111-111111111111',
  due_at_utc = '2026-11-19 23:30:00+07',
  status = 'Scheduled',
  unassigned_reason = NULL,
  revision = revision + 1,
  updated_at = NOW()
WHERE id = '660d864e-c190-4835-9abd-5376a76e1f43';

COMMIT;
