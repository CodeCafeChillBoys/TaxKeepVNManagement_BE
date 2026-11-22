using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicalStatementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_documents_documents_parent_document_id') THEN
                        ALTER TABLE documents DROP CONSTRAINT "FK_documents_documents_parent_document_id";
                    END IF;
                END $$;

                DROP INDEX IF EXISTS idx_documents_parent_document_id;
                ALTER TABLE documents DROP COLUMN IF EXISTS medical_statement_ocr;
                ALTER TABLE documents DROP COLUMN IF EXISTS parent_document_id;

                ALTER TABLE documents ADD COLUMN IF NOT EXISTS applied_threshold numeric(5,4);
                ALTER TABLE documents ADD COLUMN IF NOT EXISTS is_passed_threshold boolean;
                ALTER TABLE documents ADD COLUMN IF NOT EXISTS overall_confidence numeric(5,4);

                ALTER TABLE consultation_platform_policies ADD COLUMN IF NOT EXISTS chat_allowed_attachment_types character varying(100) NOT NULL DEFAULT 'IMAGE,PDF,DOCUMENT';
                ALTER TABLE consultation_platform_policies ADD COLUMN IF NOT EXISTS chat_history_default_page_size integer NOT NULL DEFAULT 50;
                ALTER TABLE consultation_platform_policies ADD COLUMN IF NOT EXISTS chat_history_max_page_size integer NOT NULL DEFAULT 100;
                ALTER TABLE consultation_platform_policies ADD COLUMN IF NOT EXISTS chat_max_attachment_url_length integer NOT NULL DEFAULT 1000;
                ALTER TABLE consultation_platform_policies ADD COLUMN IF NOT EXISTS chat_message_max_length integer NOT NULL DEFAULT 4000;
                ALTER TABLE consultation_platform_policies ADD COLUMN IF NOT EXISTS chat_room_url_base character varying(500) NOT NULL DEFAULT 'https://meet.jit.si';

                CREATE TABLE IF NOT EXISTS consultation_messages (
                    id uuid NOT NULL PRIMARY KEY,
                    booking_id uuid NOT NULL,
                    sender_id uuid NOT NULL,
                    sender_role character varying(50) NOT NULL,
                    sender_name character varying(255) NOT NULL,
                    sender_avatar_url character varying(1000),
                    content character varying(4000) NOT NULL,
                    attachment_url character varying(1000),
                    attachment_type character varying(50),
                    is_read boolean NOT NULL DEFAULT false,
                    created_at timestamp with time zone NOT NULL,
                    CONSTRAINT "FK_consultation_messages_bookings_booking_id" FOREIGN KEY (booking_id) REFERENCES bookings (id) ON DELETE CASCADE
                );
                """);

            migrationBuilder.CreateTable(
                name: "medical_statements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_url = table.Column<string>(type: "text", nullable: false),
                    original_filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    form_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    statement_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    patient_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    patient_dob = table.Column<DateOnly>(type: "date", nullable: true),
                    patient_gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    patient_id_card = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    insurance_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    benefit_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    hospital_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    hospital_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    department = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    admission_date = table.Column<DateOnly>(type: "date", nullable: true),
                    discharge_date = table.Column<DateOnly>(type: "date", nullable: true),
                    treatment_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    total_hospital_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    insurance_paid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    patient_paid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    patient_self_pay_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_eligible_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    overall_confidence = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    applied_threshold = table.Column<decimal>(type: "numeric(5,4)", nullable: true),
                    is_passed_threshold = table.Column<bool>(type: "boolean", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "UPLOADED"),
                    validation_errors = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_statements", x => x.id);
                    table.ForeignKey(
                        name: "FK_medical_statements_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "medical_statement_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    medical_statement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_order = table.Column<int>(type: "integer", nullable: false),
                    section_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    section_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    item_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    insurance_paid = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    patient_copay = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    patient_self_pay = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    is_eligible_deduction = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deduction_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    eligible_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_statement_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_medical_statement_lines_medical_statements_medical_statemen~",
                        column: x => x.medical_statement_id,
                        principalTable: "medical_statements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO system_configs (config_id, applies_from_year, config_key, config_value, created_at, description, is_active, updated_at)
                VALUES 
                    ('44444444-0000-0000-0000-000000000020', null, 'CHAT_MESSAGE_MAX_LENGTH', '4000', '2026-01-01 00:00:00+00', 'Số ký tự tối đa cho phép trong một tin nhắn chat tư vấn', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000021', null, 'CHAT_ALLOWED_ATTACHMENT_TYPES', 'IMAGE,PDF,DOCUMENT', '2026-01-01 00:00:00+00', 'Danh sách định dạng tệp đính kèm được phép trong chat tư vấn', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000022', null, 'CHAT_MAX_ATTACHMENT_URL_LENGTH', '1000', '2026-01-01 00:00:00+00', 'Độ dài tối đa của URL tệp đính kèm trong chat tư vấn', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000023', null, 'CHAT_HISTORY_DEFAULT_PAGE_SIZE', '50', '2026-01-01 00:00:00+00', 'Số lượng tin nhắn mặc định hiển thị trên một trang lịch sử chat', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000024', null, 'CHAT_HISTORY_MAX_PAGE_SIZE', '100', '2026-01-01 00:00:00+00', 'Số lượng tin nhắn tối đa cho phép lấy trên một trang lịch sử chat', true, '2026-01-01 00:00:00+00'),
                    ('44444444-0000-0000-0000-000000000025', null, 'CONSULTATION_ROOM_URL_BASE', 'https://meet.jit.si', '2026-01-01 00:00:00+00', 'URL gốc của hệ thống phòng họp trực tuyến', true, '2026-01-01 00:00:00+00')
                ON CONFLICT (config_id) DO NOTHING;

                CREATE INDEX IF NOT EXISTS ix_consultation_messages_booking_id ON consultation_messages (booking_id);
                CREATE INDEX IF NOT EXISTS ix_consultation_messages_created_at ON consultation_messages (created_at);
                """);

            migrationBuilder.CreateIndex(
                name: "idx_medical_statement_lines_statement_id",
                table: "medical_statement_lines",
                column: "medical_statement_id");

            migrationBuilder.CreateIndex(
                name: "idx_medical_statements_document_id",
                table: "medical_statements",
                column: "document_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consultation_messages");

            migrationBuilder.DropTable(
                name: "medical_statement_lines");

            migrationBuilder.DropTable(
                name: "medical_statements");

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

            migrationBuilder.DropColumn(
                name: "applied_threshold",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "is_passed_threshold",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "overall_confidence",
                table: "documents");

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

            migrationBuilder.AddColumn<string>(
                name: "medical_statement_ocr",
                table: "documents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_document_id",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_documents_parent_document_id",
                table: "documents",
                column: "parent_document_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_documents_parent_document_id",
                table: "documents",
                column: "parent_document_id",
                principalTable: "documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
