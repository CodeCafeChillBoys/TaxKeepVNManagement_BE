-- ============================================================
-- SEED: Cấu hình Quyết toán Thuế TNCN trong system_configs
-- Căn cứ: Luật Thuế TNCN số 109/2025/QH15 (từ 2026)
--         và Luật cũ 7 bậc (đến 2025)
-- Admin có thể UPDATE từng dòng bất cứ lúc nào qua API:
--   PUT /api/v1/tax-settlements/admin/brackets
--   PUT /api/v1/tax-settlements/admin/deductions
-- ============================================================

-- Cầu dao xác định năm áp dụng luật mới
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_NEW_LAW_FROM_YEAR',
    '2026',
    'Từ năm này trở đi áp dụng Luật 109/2025/QH15 (5 bậc). Trước đó dùng luật 7 bậc cũ.',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- ── BỘ LUẬT CŨ (≤ 2025) ─────────────────────────────────────────────────────

-- Mức giảm trừ bản thân: 11 triệu/tháng
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_DEDUCTION_PERSONAL_MONTHLY_2025',
    '11000000',
    'Giảm trừ bản thân 11.000.000 VNĐ/tháng (132 triệu/năm) — Luật cũ ≤ 2025.',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- Mức giảm trừ NPT: 4,4 triệu/người/tháng
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_DEDUCTION_DEPENDENT_MONTHLY_2025',
    '4400000',
    'Giảm trừ người phụ thuộc 4.400.000 VNĐ/người/tháng — Luật cũ ≤ 2025.',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- Biểu thuế 7 bậc lũy tiến cũ (công thức rút gọn)
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_BRACKETS_JSON_2025',
    '[
      {"bracketNo":1,"fromMonthly":0,        "toMonthly":5000000,  "rate":0.05,"quickDeduct":0},
      {"bracketNo":2,"fromMonthly":5000000,  "toMonthly":10000000, "rate":0.10,"quickDeduct":250000},
      {"bracketNo":3,"fromMonthly":10000000, "toMonthly":18000000, "rate":0.15,"quickDeduct":750000},
      {"bracketNo":4,"fromMonthly":18000000, "toMonthly":32000000, "rate":0.20,"quickDeduct":1650000},
      {"bracketNo":5,"fromMonthly":32000000, "toMonthly":52000000, "rate":0.25,"quickDeduct":3250000},
      {"bracketNo":6,"fromMonthly":52000000, "toMonthly":80000000, "rate":0.30,"quickDeduct":5850000},
      {"bracketNo":7,"fromMonthly":80000000, "toMonthly":null,     "rate":0.35,"quickDeduct":9850000}
    ]',
    'Biểu thuế TNCN lũy tiến 7 bậc — Luật cũ (áp dụng đến năm 2025). Công thức: Thuế = TNTT × Rate - QuickDeduct.',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- ── BỘ LUẬT MỚI (≥ 2026 — Luật 109/2025/QH15) ──────────────────────────────

-- Mức giảm trừ bản thân: 15,5 triệu/tháng
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_DEDUCTION_PERSONAL_MONTHLY_2026',
    '15500000',
    'Giảm trừ bản thân 15.500.000 VNĐ/tháng (186 triệu/năm) — Luật 109/2025/QH15 từ 2026.',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- Mức giảm trừ NPT: 6,2 triệu/người/tháng
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_DEDUCTION_DEPENDENT_MONTHLY_2026',
    '6200000',
    'Giảm trừ người phụ thuộc 6.200.000 VNĐ/người/tháng — Luật 109/2025/QH15 từ 2026.',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- Biểu thuế 5 bậc lũy tiến mới (Điều 22 Luật 109/2025/QH15)
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_BRACKETS_JSON_2026',
    '[
      {"bracketNo":1,"fromMonthly":0,         "toMonthly":10000000, "rate":0.05,"quickDeduct":0},
      {"bracketNo":2,"fromMonthly":10000000,  "toMonthly":30000000, "rate":0.10,"quickDeduct":500000},
      {"bracketNo":3,"fromMonthly":30000000,  "toMonthly":60000000, "rate":0.20,"quickDeduct":3500000},
      {"bracketNo":4,"fromMonthly":60000000,  "toMonthly":100000000,"rate":0.30,"quickDeduct":9500000},
      {"bracketNo":5,"fromMonthly":100000000, "toMonthly":null,     "rate":0.35,"quickDeduct":14500000}
    ]',
    'Biểu thuế TNCN lũy tiến 5 bậc — Luật 109/2025/QH15 (áp dụng từ 2026). Bậc 1: ≤10tr (5%), Bậc 2: 10-30tr (10%), Bậc 3: 30-60tr (20%), Bậc 4: 60-100tr (30%), Bậc 5: >100tr (35%).',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- ── CÁC THAM SỐ DÙNG CHUNG (Không phân biệt năm) ───────────────────────────

-- Tỷ lệ BHXH người lao động đóng: 8%
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_INSURANCE_BHXH_RATE',
    '0.08',
    'Tỷ lệ đóng BHXH bắt buộc của người lao động: 8% trên thu nhập làm căn cứ đóng BH.',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- Tỷ lệ BHYT người lao động đóng: 1,5%
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_INSURANCE_BHYT_RATE',
    '0.015',
    'Tỷ lệ đóng BHYT bắt buộc của người lao động: 1,5% trên thu nhập làm căn cứ đóng BH.',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- Tỷ lệ BHTN người lao động đóng: 1%
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_INSURANCE_BHTN_RATE',
    '0.01',
    'Tỷ lệ đóng BHTN bắt buộc của người lao động: 1% trên thu nhập làm căn cứ đóng BH.',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- Ngưỡng chênh lệch nhỏ — miễn phạt (không cần nộp thêm)
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_SMALL_AMOUNT_EXEMPTION',
    '50000',
    'Nếu số thuế nộp thiếu ≤ 50.000 VNĐ → miễn nộp bù, không bị phạt chậm nộp (quy định hành chính thuế).',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- ── CẤU HÌNH NHẮC HẠN QUYẾT TOÁN THUẾ (TaxSettlementReminderJob) ────────────

-- Các mốc ngày trước deadline để gửi thông báo nhắc nhở (cách nhau bởi dấu phẩy)
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'TAX_SETTLEMENT_REMINDER_DAYS',
    '60,30,15,7,3,1',
    'Các mốc ngày trước hạn deadline gửi thông báo nhắc người nộp thuế (VD: 60, 30, 15, 7, 3, 1 ngày).',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- Tháng hạn chót nộp quyết toán thuế (Tháng 4 theo Luật Quản lý thuế số 38/2019/QH14)
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'TAX_SETTLEMENT_DEADLINE_MONTH',
    '4',
    'Tháng hạn chót nộp hồ sơ quyết toán thuế TNCN cho cá nhân tự quyết toán (Tháng 4).',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- Ngày hạn chót nộp quyết toán thuế (Ngày 30/4)
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'TAX_SETTLEMENT_DEADLINE_DAY',
    '30',
    'Ngày hạn chót nộp hồ sơ quyết toán thuế TNCN cho cá nhân tự quyết toán (Ngày 30/4).',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- ── CHI PHÍ Y TẾ & GIÁO DỤC (Luật 109/2025/QH15 — áp dụng từ 2026) ─────────

-- Mức trần giảm trừ chi phí y tế: 23 triệu/năm
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_MEDICAL_MAX_YEARLY',
    '23000000',
    'Mức trần giảm trừ chi phí y tế tối đa 23.000.000 VNĐ/năm — Luật 109/2025/QH15 (từ 2026). Biên lai loại MEDICAL_RECEIPT đã xác nhận (CONFIRMED).',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- Mức trần giảm trừ chi phí giáo dục: 24 triệu/năm
INSERT INTO system_configs (config_id, config_key, config_value, description, is_active, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'PIT_EDUCATION_MAX_YEARLY',
    '24000000',
    'Mức trần giảm trừ chi phí giáo dục (học phí) tối đa 24.000.000 VNĐ/năm — Luật 109/2025/QH15 (từ 2026). Biên lai loại EDUCATION_RECEIPT đã xác nhận (CONFIRMED).',
    true,
    NOW(), NOW()
)
ON CONFLICT (config_key) DO UPDATE
SET config_value = EXCLUDED.config_value, updated_at = NOW();

-- ============================================================
-- KIỂM TRA kết quả seed
-- SELECT config_key, config_value, description FROM system_configs
-- ORDER BY config_key;
-- ============================================================
