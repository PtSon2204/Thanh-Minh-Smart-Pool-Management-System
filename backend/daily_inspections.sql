-- Bảng lưu kết quả kiểm tra thiết bị vận hành và an toàn đầu ca (Daily Safety Inspection)
-- Schema: smart_pool

CREATE TABLE IF NOT EXISTS smart_pool.daily_inspections (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    inspector_id UUID NOT NULL,
    shift_id UUID NOT NULL,
    inspection_date DATE NOT NULL,
    inspection_time TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    items_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    overall_status VARCHAR(50) NOT NULL DEFAULT 'Passed',
    notes VARCHAR(1000),
    created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
    CONSTRAINT daily_inspections_inspector_id_fkey FOREIGN KEY (inspector_id) REFERENCES smart_pool.users(id),
    CONSTRAINT daily_inspections_shift_id_fkey FOREIGN KEY (shift_id) REFERENCES smart_pool.shifts(id)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_daily_inspections_date_shift ON smart_pool.daily_inspections(inspection_date, shift_id);
CREATE INDEX IF NOT EXISTS idx_daily_inspections_date ON smart_pool.daily_inspections(inspection_date DESC);
CREATE INDEX IF NOT EXISTS idx_daily_inspections_status ON smart_pool.daily_inspections(overall_status);
