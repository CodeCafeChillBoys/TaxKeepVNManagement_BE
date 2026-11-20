using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicalClaimFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "applied_threshold",
                table: "documents",
                type: "numeric(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "eligible_amount",
                table: "documents",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "facility_name",
                table: "documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_insurance_card",
                table: "documents",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "insurance_benefit_rate",
                table: "documents",
                type: "numeric(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_passed_threshold",
                table: "documents",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "medical_claim_status",
                table: "documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "overall_confidence",
                table: "documents",
                type: "numeric(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_document_id",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "patient_dependent_id",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_name",
                table: "documents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "service_date",
                table: "documents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "benefit_rate",
                table: "document_items",
                type: "numeric(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deduction_reason",
                table: "document_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deduction_status",
                table: "document_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "eligible_amount",
                table: "document_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_code",
                table: "document_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_name",
                table: "document_items",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_insurance_card",
                table: "document_items",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "hospital_amount",
                table: "document_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "insurance_amount",
                table: "document_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "insurance_fund_paid",
                table: "document_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "insurance_rate",
                table: "document_items",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_package_sub_line",
                table: "document_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_user_overridden",
                table: "document_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "other_source_paid",
                table: "document_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "patient_co_pay",
                table: "document_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "patient_self_pay",
                table: "document_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "section_index",
                table: "document_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "self_pay_amount",
                table: "document_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "self_pay_opt_in",
                table: "document_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "self_pay_reason",
                table: "document_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "service_rate",
                table: "document_items",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_price_insurance",
                table: "document_items",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "chat_allowed_attachment_types",
                table: "consultation_platform_policies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "IMAGE,PDF,DOCUMENT");

            migrationBuilder.AddColumn<int>(
                name: "chat_history_default_page_size",
                table: "consultation_platform_policies",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<int>(
                name: "chat_history_max_page_size",
                table: "consultation_platform_policies",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<int>(
                name: "chat_max_attachment_url_length",
                table: "consultation_platform_policies",
                type: "integer",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.AddColumn<int>(
                name: "chat_message_max_length",
                table: "consultation_platform_policies",
                type: "integer",
                nullable: false,
                defaultValue: 4000);

            migrationBuilder.AddColumn<string>(
                name: "chat_room_url_base",
                table: "consultation_platform_policies",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "https://meet.jit.si");

            migrationBuilder.CreateTable(
                name: "bhyt_coverage_catalogs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    active_ingredient = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    legal_document_ref = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bhyt_coverage_catalogs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "consultation_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sender_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sender_avatar_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    attachment_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    attachment_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultation_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_consultation_messages_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "document_types",
                columns: new[] { "code", "description", "name" },
                values: new object[] { "MEDICAL_COST_STATEMENT", "Bảng kê chi phí KCB (Mẫu 01/KBCB theo QĐ 697/QĐ-BYT hoặc QĐ 6556/QĐ-BYT) làm căn cứ chứng minh cho Hóa đơn y tế.", "Bảng kê chi phí khám bệnh, chữa bệnh" });

            migrationBuilder.InsertData(
                table: "system_configs",
                columns: new[] { "config_id", "applies_from_year", "config_key", "config_value", "created_at", "description", "is_active", "updated_at" },
                values: new object[,]
                {
                    { new Guid("44444444-0000-0000-0000-000000000020"), null, "CHAT_MESSAGE_MAX_LENGTH", "4000", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Số ký tự tối đa cho phép trong một tin nhắn chat tư vấn", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-0000-0000-0000-000000000021"), null, "CHAT_ALLOWED_ATTACHMENT_TYPES", "IMAGE,PDF,DOCUMENT", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Danh sách định dạng tệp đính kèm được phép trong chat tư vấn", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-0000-0000-0000-000000000022"), null, "CHAT_MAX_ATTACHMENT_URL_LENGTH", "1000", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Độ dài tối đa của URL tệp đính kèm trong chat tư vấn", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-0000-0000-0000-000000000023"), null, "CHAT_HISTORY_DEFAULT_PAGE_SIZE", "50", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Số lượng tin nhắn mặc định hiển thị trên một trang lịch sử chat", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-0000-0000-0000-000000000024"), null, "CHAT_HISTORY_MAX_PAGE_SIZE", "100", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Số lượng tin nhắn tối đa cho phép lấy trên một trang lịch sử chat", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-0000-0000-0000-000000000025"), null, "CONSULTATION_ROOM_URL_BASE", "https://meet.jit.si", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "URL gốc của hệ thống phòng họp trực tuyến", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-0000-0000-0000-000000000026"), null, "MEDICAL_REQUIRE_STATEMENT", "true", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bắt buộc phải có Bảng kê chi phí KCB (Mẫu 01/KBCB) kèm theo hóa đơn y tế mới được giảm trừ", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-0000-0000-0000-000000000027"), null, "MEDICAL_NO_CARD_EXCLUDED_GROUP_CODES", "12", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Danh sách mã mục bị loại trừ ngay khi khám không dùng thẻ BHYT (VD: 12 - Dịch vụ khác)", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-0000-0000-0000-000000000028"), null, "MEDICAL_STATEMENT_REMINDER_DAYS", "7", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Chu kỳ số ngày nhắc người dùng bổ sung Bảng kê chi phí KCB cho hóa đơn y tế", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-0000-0000-0000-000000000029"), null, "MEDICAL_STATEMENT_AMOUNT_TOLERANCE", "1000", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sai lệch tiền tối đa cho phép (VNĐ) khi ghép tự động bảng kê với hóa đơn và kiểm tra chéo", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("44444444-0000-0000-0000-000000000030"), null, "MEDICAL_CATALOG_MATCH_MIN_SCORE", "0.9", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ngưỡng điểm tin cậy tối thiểu khi tra cứu đối soát danh mục BHYT cho trường hợp không dùng thẻ", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "idx_documents_parent_document_id",
                table: "documents",
                column: "parent_document_id");

            migrationBuilder.CreateIndex(
                name: "idx_documents_patient_dependent_id",
                table: "documents",
                column: "patient_dependent_id");

            migrationBuilder.CreateIndex(
                name: "idx_bhyt_catalog_code",
                table: "bhyt_coverage_catalogs",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "idx_bhyt_catalog_normalized_name",
                table: "bhyt_coverage_catalogs",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "idx_bhyt_catalog_type_code",
                table: "bhyt_coverage_catalogs",
                columns: new[] { "catalog_type", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_consultation_messages_booking_id",
                table: "consultation_messages",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_consultation_messages_created_at",
                table: "consultation_messages",
                column: "created_at");

            migrationBuilder.AddForeignKey(
                name: "FK_documents_dependents_patient_dependent_id",
                table: "documents",
                column: "patient_dependent_id",
                principalTable: "dependents",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_documents_parent_document_id",
                table: "documents",
                column: "parent_document_id",
                principalTable: "documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_dependents_patient_dependent_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_documents_documents_parent_document_id",
                table: "documents");

            migrationBuilder.DropTable(
                name: "bhyt_coverage_catalogs");

            migrationBuilder.DropTable(
                name: "consultation_messages");

            migrationBuilder.DropIndex(
                name: "idx_documents_parent_document_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "idx_documents_patient_dependent_id",
                table: "documents");

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "code",
                keyValue: "MEDICAL_COST_STATEMENT");

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000020"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000022"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000025"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000026"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000027"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000028"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000029"));

            migrationBuilder.DeleteData(
                table: "system_configs",
                keyColumn: "config_id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000030"));

            migrationBuilder.DropColumn(
                name: "applied_threshold",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "eligible_amount",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "facility_name",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "has_insurance_card",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "insurance_benefit_rate",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "is_passed_threshold",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "medical_claim_status",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "overall_confidence",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "parent_document_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "patient_dependent_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "patient_name",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "service_date",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "benefit_rate",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "deduction_reason",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "deduction_status",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "eligible_amount",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "group_code",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "group_name",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "has_insurance_card",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "hospital_amount",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "insurance_amount",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "insurance_fund_paid",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "insurance_rate",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "is_package_sub_line",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "is_user_overridden",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "other_source_paid",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "patient_co_pay",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "patient_self_pay",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "section_index",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "self_pay_amount",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "self_pay_opt_in",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "self_pay_reason",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "service_rate",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "unit_price_insurance",
                table: "document_items");

            migrationBuilder.DropColumn(
                name: "chat_allowed_attachment_types",
                table: "consultation_platform_policies");

            migrationBuilder.DropColumn(
                name: "chat_history_default_page_size",
                table: "consultation_platform_policies");

            migrationBuilder.DropColumn(
                name: "chat_history_max_page_size",
                table: "consultation_platform_policies");

            migrationBuilder.DropColumn(
                name: "chat_max_attachment_url_length",
                table: "consultation_platform_policies");

            migrationBuilder.DropColumn(
                name: "chat_message_max_length",
                table: "consultation_platform_policies");

            migrationBuilder.DropColumn(
                name: "chat_room_url_base",
                table: "consultation_platform_policies");
        }
    }
}
