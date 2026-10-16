using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitSystemConfigsIntoDomainTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            

            

            

            migrationBuilder.CreateTable(
                name: "consultation_platform_policies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expert_min_fee_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    expert_max_fee_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    booking_slot_hold_minutes = table.Column<int>(type: "integer", nullable: false),
                    booking_min_lead_time_hours = table.Column<int>(type: "integer", nullable: false),
                    expert_confirm_timeout_hours = table.Column<int>(type: "integer", nullable: false),
                    platform_commission_rate_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    session_open_before_minutes = table.Column<int>(type: "integer", nullable: false),
                    session_no_show_minutes = table.Column<int>(type: "integer", nullable: false),
                    rating_window_days = table.Column<int>(type: "integer", nullable: false),
                    escrow_cooldown_hours = table.Column<int>(type: "integer", nullable: false),
                    cancel_full_refund_hours = table.Column<int>(type: "integer", nullable: false),
                    cancel_partial_refund_hours = table.Column<int>(type: "integer", nullable: false),
                    cancel_partial_refund_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    expert_max_daily_slots = table.Column<int>(type: "integer", nullable: false),
                    expert_min_withdrawal_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    expert_late_cancel_hours = table.Column<int>(type: "integer", nullable: false),
                    expert_late_cancel_penalty_vnd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    dispute_window_hours = table.Column<int>(type: "integer", nullable: false),
                    dispute_expert_reply_hours = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultation_platform_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_brackets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bracket_no = table.Column<int>(type: "integer", nullable: false),
                    from_monthly = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    to_monthly = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    quick_deduct = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    applies_from_year = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_brackets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_deduction_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    applies_from_year = table.Column<int>(type: "integer", nullable: false),
                    law_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    personal_monthly = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    dependent_monthly = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    bhxh_rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    bhyt_rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    bhtn_rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    small_amount_exemption = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    medical_max_yearly = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    education_max_yearly = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_deduction_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_settlement_schedules",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tax_year = table.Column<int>(type: "integer", nullable: true),
                    deadline_month = table.Column<int>(type: "integer", nullable: false),
                    deadline_day = table.Column<int>(type: "integer", nullable: false),
                    reminder_days = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_settlement_schedules", x => x.id);
                });

            migrationBuilder.UpdateData(
                table: "specializations",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "code", "description", "name" },
                values: new object[] { "MULTI_SOURCE_SALARY_PIT", "Tư vấn quyết toán thuế TNCN từ tiền lương, tiền công tại nhiều nơi chi trả trong năm", "Quyết toán thuế TNCN đa nguồn thu nhập" });

            migrationBuilder.UpdateData(
                table: "specializations",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "code", "description", "name" },
                values: new object[] { "DEPENDENT_DEDUCTION_TT87", "Rà soát điều kiện và bộ chứng từ chứng minh người phụ thuộc hợp lệ", "Tối ưu giảm trừ gia cảnh & Hồ sơ Người phụ thuộc (Thông tư 87/2026/TT-BTC)" });

            migrationBuilder.UpdateData(
                table: "specializations",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "code", "description", "name" },
                values: new object[] { "MEDICAL_EDUCATION_DEDUCTION", "Kiểm tra tính hợp pháp của hóa đơn viện phí, bảng kê Bộ Y tế và học phí", "Thẩm định chứng từ giảm trừ Y tế & Giáo dục (Luật 109/2025/QH15 & NĐ 253/2026)" });

            migrationBuilder.UpdateData(
                table: "specializations",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "code", "description", "name" },
                values: new object[] { "TAX_EXEMPT_OVERTIME_ALLOWANCE", "Bóc tách phần chênh lệch lương làm thêm giờ, làm đêm được miễn thuế TNCN", "Tư vấn bóc tách thu nhập miễn thuế (Lương làm thêm giờ OT, làm đêm, phụ cấp)" });

            migrationBuilder.UpdateData(
                table: "specializations",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "code", "description", "name" },
                values: new object[] { "PIT_REFUND_EXPLANATION", "Hướng dẫn lập hồ sơ hoàn thuế Mẫu 02/QTT-TNCN và giải trình số liệu với cơ quan thuế", "Thủ tục Hoàn thuế TNCN nộp thừa & Giải trình cơ quan thuế" });

            // ── TỰ ĐỘNG CHUYỂN DỮ LIỆU TỪ system_configs SANG CÁC BẢNG LIÊN QUAN ──
            migrationBuilder.Sql(@"
INSERT INTO tax_brackets (id, bracket_no, from_monthly, to_monthly, rate, quick_deduct, applies_from_year, description, is_active, created_at, updated_at) VALUES
('11111111-0000-0000-0000-000000000001', 1, 0, 5000000, 0.05, 0, 2025, 'Bậc 1: Đến 5 triệu đồng (5%)', true, NOW(), NOW()),
('11111111-0000-0000-0000-000000000002', 2, 5000000, 10000000, 0.10, 250000, 2025, 'Bậc 2: Trên 5 đến 10 triệu đồng (10%)', true, NOW(), NOW()),
('11111111-0000-0000-0000-000000000003', 3, 10000000, 18000000, 0.15, 750000, 2025, 'Bậc 3: Trên 10 đến 18 triệu đồng (15%)', true, NOW(), NOW()),
('11111111-0000-0000-0000-000000000004', 4, 18000000, 32000000, 0.20, 1650000, 2025, 'Bậc 4: Trên 18 đến 32 triệu đồng (20%)', true, NOW(), NOW()),
('11111111-0000-0000-0000-000000000005', 5, 32000000, 52000000, 0.25, 3250000, 2025, 'Bậc 5: Trên 32 đến 52 triệu đồng (25%)', true, NOW(), NOW()),
('11111111-0000-0000-0000-000000000006', 6, 52000000, 80000000, 0.30, 5850000, 2025, 'Bậc 6: Trên 52 đến 80 triệu đồng (30%)', true, NOW(), NOW()),
('11111111-0000-0000-0000-000000000007', 7, 80000000, NULL, 0.35, 9850000, 2025, 'Bậc 7: Trên 80 triệu đồng (35%)', true, NOW(), NOW()),
('22222222-0000-0000-0000-000000000001', 1, 0, 10000000, 0.05, 0, 2026, 'Bậc 1: Đến 10 triệu đồng (5%)', true, NOW(), NOW()),
('22222222-0000-0000-0000-000000000002', 2, 10000000, 30000000, 0.10, 500000, 2026, 'Bậc 2: Trên 10 đến 30 triệu đồng (10%)', true, NOW(), NOW()),
('22222222-0000-0000-0000-000000000003', 3, 30000000, 60000000, 0.20, 3500000, 2026, 'Bậc 3: Trên 30 đến 60 triệu đồng (20%)', true, NOW(), NOW()),
('22222222-0000-0000-0000-000000000004', 4, 60000000, 100000000, 0.30, 9500000, 2026, 'Bậc 4: Trên 60 đến 100 triệu đồng (30%)', true, NOW(), NOW()),
('22222222-0000-0000-0000-000000000005', 5, 100000000, NULL, 0.35, 14500000, 2026, 'Bậc 5: Trên 100 triệu đồng (35%)', true, NOW(), NOW())
ON CONFLICT (id) DO UPDATE SET
  from_monthly = EXCLUDED.from_monthly,
  to_monthly = EXCLUDED.to_monthly,
  rate = EXCLUDED.rate,
  quick_deduct = EXCLUDED.quick_deduct,
  description = EXCLUDED.description,
  updated_at = NOW();

INSERT INTO tax_deduction_policies (id, applies_from_year, law_name, personal_monthly, dependent_monthly, bhxh_rate, bhyt_rate, bhtn_rate, small_amount_exemption, medical_max_yearly, education_max_yearly, is_active, created_at, updated_at) VALUES
('33333333-0000-0000-0000-000000002025', 2025, 'Luật Thuế TNCN (áp dụng ≤ 2025) — 7 bậc thuế', 11000000, 4400000, 0.08, 0.015, 0.01, 50000, 0, 0, true, NOW(), NOW()),
('33333333-0000-0000-0000-000000002026', 2026, 'Luật 109/2025/QH15 (áp dụng từ 2026) — 5 bậc thuế', 15500000, 6200000, 0.08, 0.015, 0.01, 50000, 23000000, 24000000, true, NOW(), NOW())
ON CONFLICT (id) DO UPDATE SET
  law_name = EXCLUDED.law_name,
  personal_monthly = EXCLUDED.personal_monthly,
  dependent_monthly = EXCLUDED.dependent_monthly,
  bhxh_rate = EXCLUDED.bhxh_rate,
  bhyt_rate = EXCLUDED.bhyt_rate,
  bhtn_rate = EXCLUDED.bhtn_rate,
  small_amount_exemption = EXCLUDED.small_amount_exemption,
  medical_max_yearly = EXCLUDED.medical_max_yearly,
  education_max_yearly = EXCLUDED.education_max_yearly,
  updated_at = NOW();

INSERT INTO consultation_platform_policies (id, expert_min_fee_vnd, expert_max_fee_vnd, booking_slot_hold_minutes, booking_min_lead_time_hours, expert_confirm_timeout_hours, platform_commission_rate_percent, session_open_before_minutes, session_no_show_minutes, rating_window_days, escrow_cooldown_hours, cancel_full_refund_hours, cancel_partial_refund_hours, cancel_partial_refund_percent, expert_max_daily_slots, expert_min_withdrawal_vnd, expert_late_cancel_hours, expert_late_cancel_penalty_vnd, dispute_window_hours, dispute_expert_reply_hours, is_active, created_at, updated_at) VALUES
(1, 100000, 2000000, 10, 4, 2, 20.00, 10, 15, 7, 24, 24, 4, 50.00, 6, 500000, 12, 100000, 24, 24, true, NOW(), NOW())
ON CONFLICT (id) DO NOTHING;

INSERT INTO tax_settlement_schedules (id, tax_year, deadline_month, deadline_day, reminder_days, description, is_active, created_at, updated_at) VALUES
(1, NULL, 4, 30, '60,30,15,7,3,1', 'Hạn chót quyết toán thuế TNCN theo Luật Quản lý thuế số 38/2019/QH14 (Ngày 30/4)', true, NOW(), NOW())
ON CONFLICT (id) DO NOTHING;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consultation_platform_policies");

            migrationBuilder.DropTable(
                name: "tax_brackets");

            migrationBuilder.DropTable(
                name: "tax_deduction_policies");

            migrationBuilder.DropTable(
                name: "tax_settlement_schedules");

            migrationBuilder.UpdateData(
                table: "specializations",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "code", "description", "name" },
                values: new object[] { "PIT", null, "Thuế thu nhập cá nhân (TNCN)" });

            migrationBuilder.UpdateData(
                table: "specializations",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "code", "description", "name" },
                values: new object[] { "CIT", null, "Thuế thu nhập doanh nghiệp (TNDN)" });

            migrationBuilder.UpdateData(
                table: "specializations",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "code", "description", "name" },
                values: new object[] { "FINALIZATION", null, "Quyết toán thuế" });

            migrationBuilder.UpdateData(
                table: "specializations",
                keyColumn: "id",
                keyValue: 4,
                columns: new[] { "code", "description", "name" },
                values: new object[] { "TAX_REFUND", null, "Hoàn thuế" });

            migrationBuilder.UpdateData(
                table: "specializations",
                keyColumn: "id",
                keyValue: 5,
                columns: new[] { "code", "description", "name" },
                values: new object[] { "INTERNAL_ACCOUNTING", null, "Kế toán nội bộ" });
        }
    }
}
