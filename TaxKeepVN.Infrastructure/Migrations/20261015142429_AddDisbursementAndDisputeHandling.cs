using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDisbursementAndDisputeHandling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_disputes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expert_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    evidence_urls_json = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "PENDING_EXPERT_RESPONSE"),
                    expert_response = table.Column<string>(type: "text", nullable: true),
                    expert_evidence_urls_json = table.Column<string>(type: "text", nullable: true),
                    expert_responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expert_response_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    admin_decision_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    admin_resolution = table.Column<string>(type: "text", nullable: true),
                    refund_rate_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    user_refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    expert_settlement_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    platform_fee_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    admin_notes = table.Column<string>(type: "text", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_disputes", x => x.id);
                    table.ForeignKey(
                        name: "FK_booking_disputes_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_booking_disputes_users_expert_user_id",
                        column: x => x.expert_user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_booking_disputes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS consultation_sessions (
                    id uuid NOT NULL,
                    booking_id uuid NOT NULL,
                    room_name character varying(150) NOT NULL,
                    room_url character varying(500) NOT NULL,
                    user_check_in_at timestamp with time zone,
                    expert_check_in_at timestamp with time zone,
                    started_at timestamp with time zone,
                    ended_at timestamp with time zone,
                    actual_duration_minutes integer,
                    shared_notes text,
                    no_show_type character varying(50) NOT NULL DEFAULT 'NONE',
                    no_show_reported_by character varying(50),
                    no_show_reported_at timestamp with time zone,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_consultation_sessions"" PRIMARY KEY (id)
                );
                CREATE UNIQUE INDEX IF NOT EXISTS uq_consultation_sessions_booking_id ON consultation_sessions (booking_id);

                CREATE TABLE IF NOT EXISTS escrow_transactions (
                    id uuid NOT NULL,
                    booking_id uuid NOT NULL,
                    order_code bigint NOT NULL,
                    amount numeric(18,2) NOT NULL,
                    commission_rate numeric(5,2) NOT NULL,
                    commission_amount numeric(18,2) NOT NULL,
                    expert_receivable numeric(18,2) NOT NULL,
                    escrow_status character varying(50) NOT NULL DEFAULT 'PendingPayment',
                    payment_gateway character varying(50) NOT NULL DEFAULT 'VNPAY',
                    payment_reference character varying(100),
                    payment_link_id character varying(100),
                    checkout_url text,
                    qr_code text,
                    refund_amount numeric(18,2) NOT NULL DEFAULT 0,
                    refund_reason text,
                    paid_at timestamp with time zone,
                    settled_at timestamp with time zone,
                    refunded_at timestamp with time zone,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_escrow_transactions"" PRIMARY KEY (id)
                );
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'FK_escrow_transactions_bookings_booking_id'
                    ) THEN
                        ALTER TABLE escrow_transactions
                        ADD CONSTRAINT ""FK_escrow_transactions_bookings_booking_id""
                        FOREIGN KEY (booking_id) REFERENCES bookings (id) ON DELETE CASCADE;
                    END IF;
                END $$;
                CREATE INDEX IF NOT EXISTS idx_escrow_transactions_status ON escrow_transactions (escrow_status);
                CREATE UNIQUE INDEX IF NOT EXISTS uq_escrow_transactions_booking_id ON escrow_transactions (booking_id);
                CREATE UNIQUE INDEX IF NOT EXISTS uq_escrow_transactions_order_code ON escrow_transactions (order_code);
            ");


            migrationBuilder.CreateTable(
                name: "user_wallets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "USER"),
                    available_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    frozen_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    total_earned = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    total_withdrawn = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_wallets", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_wallets_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "withdrawal_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    account_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    transaction_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    admin_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    processed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withdrawal_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_withdrawal_requests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wallet_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    balance_before = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_wallet_transactions_user_wallets_wallet_id",
                        column: x => x.wallet_id,
                        principalTable: "user_wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wallet_transactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.Sql(@"
                INSERT INTO system_configs (config_id, applies_from_year, config_key, config_value, created_at, description, is_active, updated_at)
                VALUES
                    ('44444444-0000-0000-0000-000000000001', NULL, 'EXPERT_MIN_FEE_VND', '100000', '2026-01-01 00:00:00+00', 'Mức phí tư vấn sàn tối thiểu cho 1 phiên (VNĐ)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000002', NULL, 'EXPERT_MAX_FEE_VND', '2000000', '2026-01-01 00:00:00+00', 'Mức phí tư vấn trần tối đa cho 1 phiên (VNĐ)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000003', NULL, 'BOOKING_SLOT_HOLD_MINUTES', '10', '2026-01-01 00:00:00+00', 'Thời gian tạm khóa slot chờ người nộp thuế hoàn tất thanh toán (Phút)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000004', NULL, 'BOOKING_MIN_LEAD_TIME_HOURS', '4', '2026-01-01 00:00:00+00', 'Thời gian tối thiểu phải đặt lịch trước giờ hẹn (Giờ)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000005', NULL, 'EXPERT_CONFIRM_TIMEOUT_HOURS', '2', '2026-01-01 00:00:00+00', 'Thời hạn tối đa để Chuyên gia Chấp nhận/Từ chối yêu cầu đặt lịch (Giờ)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000006', NULL, 'PLATFORM_COMMISSION_RATE_PERCENT', '20', '2026-01-01 00:00:00+00', 'Tỷ lệ phí hoa hồng nền tảng (A%) trích từ giá trị ca tư vấn (%)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000007', NULL, 'SESSION_OPEN_BEFORE_MINUTES', '10', '2026-01-01 00:00:00+00', 'Thời gian mở phòng tư vấn trước giờ hẹn chính thức (Phút)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000008', NULL, 'SESSION_NO_SHOW_MINUTES', '15', '2026-01-01 00:00:00+00', 'Ngưỡng thời gian xác định vắng mặt không phép No-Show (Phút)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000009', NULL, 'RATING_WINDOW_DAYS', '7', '2026-01-01 00:00:00+00', 'Thời hạn cho phép người nộp thuế gửi đánh giá sau khi kết thúc phiên (Ngày)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000010', NULL, 'ESCROW_COOLDOWN_HOURS', '24', '2026-01-01 00:00:00+00', 'Thời gian tạm giữ tiền chờ khiếu nại trước khi tự động giải ngân (Giờ)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000011', NULL, 'CANCEL_FULL_REFUND_HOURS', '24', '2026-01-01 00:00:00+00', 'Mốc hủy lịch sớm được hoàn tiền 100% trước giờ hẹn (Giờ)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000012', NULL, 'CANCEL_PARTIAL_REFUND_HOURS', '4', '2026-01-01 00:00:00+00', 'Mốc hủy lịch cận giờ được hoàn một phần trước giờ hẹn (Giờ)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000013', NULL, 'CANCEL_PARTIAL_REFUND_PERCENT', '50', '2026-01-01 00:00:00+00', 'Tỷ lệ phần trăm tiền hoàn lại khi hủy lịch cận giờ (%)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000014', NULL, 'EXPERT_MAX_DAILY_SLOTS', '6', '2026-01-01 00:00:00+00', 'Số lượng ca tư vấn tối đa Chuyên gia được mở trong 1 ngày (Ca/ngày)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000015', NULL, 'EXPERT_MIN_WITHDRAWAL_VND', '500000', '2026-01-01 00:00:00+00', 'Số tiền tối thiểu cho mỗi lệnh rút tiền từ Ví thu nhập chuyên gia (VNĐ)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000016', NULL, 'DISPUTE_WINDOW_HOURS', '24', '2026-01-01 00:00:00+00', 'Thời hạn cho phép người nộp thuế gửi khiếu nại sau khi kết thúc ca (Giờ)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000017', NULL, 'DISPUTE_EXPERT_REPLY_HOURS', '24', '2026-01-01 00:00:00+00', 'Thời hạn Chuyên gia phải gửi phản biện khiếu nại (Giờ)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000018', NULL, 'EXPERT_LATE_CANCEL_HOURS', '12', '2026-01-01 00:00:00+00', 'Mốc thời gian hủy sát giờ của Chuyên gia bị ghi nhận vi phạm/phạt (Giờ)', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000019', NULL, 'EXPERT_LATE_CANCEL_PENALTY_VND', '100000', '2026-01-01 00:00:00+00', 'Mức tiền phạt Chuyên gia khi hủy lịch sát giờ hẹn (VNĐ)', true, '2026-01-01 00:00:00+00')
                ON CONFLICT (config_id) DO UPDATE SET config_value = EXCLUDED.config_value, description = EXCLUDED.description, is_active = EXCLUDED.is_active;
            ");


            migrationBuilder.CreateIndex(
                name: "idx_booking_disputes_status",
                table: "booking_disputes",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_booking_disputes_expert_user_id",
                table: "booking_disputes",
                column: "expert_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_disputes_user_id",
                table: "booking_disputes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_booking_disputes_booking_id",
                table: "booking_disputes",
                column: "booking_id",
                unique: true);


            migrationBuilder.CreateIndex(
                name: "uq_user_wallets_user_type",
                table: "user_wallets",
                columns: new[] { "user_id", "wallet_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_wallet_transactions_user_id",
                table: "wallet_transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_wallet_transactions_wallet_id",
                table: "wallet_transactions",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "idx_withdrawal_requests_status",
                table: "withdrawal_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_withdrawal_requests_user_id",
                table: "withdrawal_requests",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_disputes");

            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS escrow_transactions CASCADE;
                DROP TABLE IF EXISTS consultation_sessions CASCADE;
            ");

            migrationBuilder.DropTable(
                name: "wallet_transactions");

            migrationBuilder.DropTable(
                name: "withdrawal_requests");

            migrationBuilder.DropTable(
                name: "user_wallets");

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000019"));

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
