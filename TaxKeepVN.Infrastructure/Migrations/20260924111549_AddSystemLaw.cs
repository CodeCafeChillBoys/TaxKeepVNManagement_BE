using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemLaw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.CreateTable(
                name: "law_rule_definitions",
                columns: table => new
                {
                    rule_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rule_group = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    value_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    default_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    required_for_flow3 = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    required_from_tax_year = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_law_rule_definitions", x => x.rule_code);
                });

            migrationBuilder.CreateTable(
                name: "legal_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    number_normalized = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    document_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    title = table.Column<string>(type: "text", nullable: true),
                    issuer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    issued_date = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    original_filename = table.Column<string>(type: "text", nullable: true),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    total_pages = table.Column<int>(type: "integer", nullable: true),
                    legal_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "CHUA_RO"),
                    legal_status_note = table.Column<string>(type: "text", nullable: true),
                    is_placeholder = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "law_changesets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    base_revision_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "EXTRACTING"),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "AI"),
                    ai_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ai_model = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ai_raw_response = table.Column<string>(type: "jsonb", nullable: true),
                    ai_warnings = table.Column<string>(type: "jsonb", nullable: true),
                    ai_error_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ai_error_message = table.Column<string>(type: "text", nullable: true),
                    pages_read = table.Column<int>(type: "integer", nullable: true),
                    total_pages = table.Column<int>(type: "integer", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    merged_revision_no = table.Column<int>(type: "integer", nullable: true),
                    merged_by = table.Column<Guid>(type: "uuid", nullable: true),
                    merged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_reason = table.Column<string>(type: "text", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_law_changesets", x => x.id);
                    table.ForeignKey(
                        name: "FK_law_changesets_legal_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "legal_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "law_change_ops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    changeset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    op_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    op_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rule_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    new_code = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    proposed_definition = table.Column<string>(type: "jsonb", nullable: true),
                    before = table.Column<string>(type: "jsonb", nullable: true),
                    after = table.Column<string>(type: "jsonb", nullable: true),
                    apply_from = table.Column<DateOnly>(type: "date", nullable: true),
                    apply_to = table.Column<DateOnly>(type: "date", nullable: true),
                    apply_basis = table.Column<string>(type: "jsonb", nullable: true),
                    article = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    clause = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    point = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    page = table.Column<int>(type: "integer", nullable: true),
                    evidence_text = table.Column<string>(type: "text", nullable: true),
                    confidence = table.Column<decimal>(type: "numeric(4,3)", nullable: true),
                    rationale = table.Column<string>(type: "text", nullable: true),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "AI"),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    edited_by_admin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    admin_note = table.Column<string>(type: "text", nullable: true),
                    conflict_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "NONE"),
                    flags = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_law_change_ops", x => x.id);
                    table.ForeignKey(
                        name: "FK_law_change_ops_law_changesets_changeset_id",
                        column: x => x.changeset_id,
                        principalTable: "law_changesets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "law_changeset_relations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    changeset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    relation_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_article = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    target_clause = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    target_point = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_article = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_clause = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_point = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_page = table.Column<int>(type: "integer", nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    evidence_text = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "AI"),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    conflict_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "NONE"),
                    conflict_detail = table.Column<string>(type: "jsonb", nullable: true),
                    target_doc_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_law_changeset_relations", x => x.id);
                    table.ForeignKey(
                        name: "FK_law_changeset_relations_law_changesets_changeset_id",
                        column: x => x.changeset_id,
                        principalTable: "law_changesets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_law_changeset_relations_legal_documents_target_doc_id",
                        column: x => x.target_doc_id,
                        principalTable: "legal_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "law_revisions",
                columns: table => new
                {
                    revision_no = table.Column<int>(type: "integer", nullable: false),
                    changeset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    committed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    committed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    meta_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_law_revisions", x => x.revision_no);
                    table.ForeignKey(
                        name: "FK_law_revisions_law_changesets_changeset_id",
                        column: x => x.changeset_id,
                        principalTable: "law_changesets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_law_revisions_legal_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "legal_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "law_rule_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_in_revision = table.Column<int>(type: "integer", nullable: false),
                    rule_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    article = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    clause = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    point = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    page = table.Column<int>(type: "integer", nullable: true),
                    evidence_text = table.Column<string>(type: "text", nullable: true),
                    apply_from = table.Column<DateOnly>(type: "date", nullable: false),
                    apply_to = table.Column<DateOnly>(type: "date", nullable: true),
                    rule_value = table.Column<string>(type: "jsonb", nullable: false),
                    superseded_in_revision = table.Column<int>(type: "integer", nullable: true),
                    superseded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    derived_from_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_op_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_law_rule_versions", x => x.id);
                    table.CheckConstraint("chk_law_rule_versions_apply_range", "apply_to IS NULL OR apply_to > apply_from");
                    table.ForeignKey(
                        name: "FK_law_rule_versions_law_change_ops_source_op_id",
                        column: x => x.source_op_id,
                        principalTable: "law_change_ops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_law_rule_versions_law_revisions_created_in_revision",
                        column: x => x.created_in_revision,
                        principalTable: "law_revisions",
                        principalColumn: "revision_no",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_law_rule_versions_law_revisions_superseded_in_revision",
                        column: x => x.superseded_in_revision,
                        principalTable: "law_revisions",
                        principalColumn: "revision_no",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_law_rule_versions_law_rule_definitions_rule_code",
                        column: x => x.rule_code,
                        principalTable: "law_rule_definitions",
                        principalColumn: "rule_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_law_rule_versions_legal_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "legal_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "legal_document_relations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_article = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    target_clause = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    target_point = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_article = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_clause = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_point = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_page = table.Column<int>(type: "integer", nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    evidence_text = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_in_revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_document_relations", x => x.id);
                    table.ForeignKey(
                        name: "FK_legal_document_relations_law_revisions_created_in_revision",
                        column: x => x.created_in_revision,
                        principalTable: "law_revisions",
                        principalColumn: "revision_no",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_legal_document_relations_legal_documents_source_document_id",
                        column: x => x.source_document_id,
                        principalTable: "legal_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_legal_document_relations_legal_documents_target_document_id",
                        column: x => x.target_document_id,
                        principalTable: "legal_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "law_orphan_resolutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    changeset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    generated_op_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_law_orphan_resolutions", x => x.id);
                    table.ForeignKey(
                        name: "FK_law_orphan_resolutions_law_change_ops_generated_op_id",
                        column: x => x.generated_op_id,
                        principalTable: "law_change_ops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_law_orphan_resolutions_law_changesets_changeset_id",
                        column: x => x.changeset_id,
                        principalTable: "law_changesets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_law_orphan_resolutions_law_rule_versions_version_id",
                        column: x => x.version_id,
                        principalTable: "law_rule_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "law_rule_definitions",
                columns: new[] { "rule_code", "created_at", "default_unit", "description", "display_name", "is_active", "required_from_tax_year", "rule_group", "updated_at", "value_kind" },
                values: new object[] { "PIT_DEDUCTION_CHARITY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Cho phép trừ các khoản đóng góp từ thiện, nhân đạo, khuyến học theo thực tế phát sinh", "Trừ đóng góp từ thiện, nhân đạo, khuyến học", true, null, "DEDUCTION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "FLAG" });

            migrationBuilder.InsertData(
                table: "law_rule_definitions",
                columns: new[] { "rule_code", "created_at", "default_unit", "description", "display_name", "is_active", "required_for_flow3", "required_from_tax_year", "rule_group", "updated_at", "value_kind" },
                values: new object[,]
                {
                    { "PIT_DEDUCTION_DEPENDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND/person/month", "Mức giảm trừ cho mỗi người phụ thuộc hợp lệ mỗi tháng", "Giảm trừ mỗi người phụ thuộc", true, true, null, "DEDUCTION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "AMOUNT" },
                    { "PIT_DEDUCTION_EDUCATION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND/year", "Mức giảm trừ chi phí học tập, đào tạo cho bản thân và người phụ thuộc tối đa trong năm", "Giảm trừ chi phí giáo dục, đào tạo", true, true, 2026, "DEDUCTION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "AMOUNT" }
                });

            migrationBuilder.InsertData(
                table: "law_rule_definitions",
                columns: new[] { "rule_code", "created_at", "default_unit", "description", "display_name", "is_active", "required_from_tax_year", "rule_group", "updated_at", "value_kind" },
                values: new object[] { "PIT_DEDUCTION_MANDATORY_INSURANCE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Cho phép trừ các khoản đóng bảo hiểm bắt buộc (BHXH, BHYT, BHTN) theo thực tế phát sinh", "Trừ bảo hiểm bắt buộc theo thực đóng", true, null, "DEDUCTION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "FLAG" });

            migrationBuilder.InsertData(
                table: "law_rule_definitions",
                columns: new[] { "rule_code", "created_at", "default_unit", "description", "display_name", "is_active", "required_for_flow3", "required_from_tax_year", "rule_group", "updated_at", "value_kind" },
                values: new object[,]
                {
                    { "PIT_DEDUCTION_MEDICAL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND/year", "Mức giảm trừ chi phí khám chữa bệnh hiểm nghèo tối đa trong năm", "Giảm trừ chi phí y tế", true, true, 2026, "DEDUCTION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "AMOUNT" },
                    { "PIT_DEDUCTION_PERSONAL", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND/month", "Mức giảm trừ gia cảnh cho chính người nộp thuế mỗi tháng", "Giảm trừ bản thân", true, true, null, "DEDUCTION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "AMOUNT" }
                });

            migrationBuilder.InsertData(
                table: "law_rule_definitions",
                columns: new[] { "rule_code", "created_at", "default_unit", "description", "display_name", "is_active", "required_from_tax_year", "rule_group", "updated_at", "value_kind" },
                values: new object[] { "PIT_DEDUCTION_VOLUNTARY_INSURANCE_CAP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND/month", "Mức đóng vào quỹ hưu trí tự nguyện, bảo hiểm bổ sung được trừ tối đa mỗi tháng", "Trần trừ bảo hiểm hưu trí bổ sung, tự nguyện, nhân thọ", true, null, "DEDUCTION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "AMOUNT" });

            migrationBuilder.InsertData(
                table: "law_rule_definitions",
                columns: new[] { "rule_code", "created_at", "default_unit", "description", "display_name", "is_active", "required_for_flow3", "required_from_tax_year", "rule_group", "updated_at", "value_kind" },
                values: new object[,]
                {
                    { "PIT_DEPENDENT_GROUPS", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Nhóm người phụ thuộc hợp lệ, độ tuổi, điều kiện khuyết tật, học tập và thu nhập", "Nhóm người phụ thuộc và điều kiện", true, true, null, "DEPENDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "JSON" },
                    { "PIT_DEPENDENT_MAX_MONTHLY_INCOME", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND/month", "Ngưỡng thu nhập bình quân tháng tối đa để đủ điều kiện làm người phụ thuộc", "Thu nhập bình quân tháng tối đa của người phụ thuộc", true, true, null, "DEPENDENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "AMOUNT" }
                });

            migrationBuilder.InsertData(
                table: "law_rule_definitions",
                columns: new[] { "rule_code", "created_at", "default_unit", "description", "display_name", "is_active", "required_from_tax_year", "rule_group", "updated_at", "value_kind" },
                values: new object[,]
                {
                    { "PIT_EXEMPTION_INSURANCE_COMPENSATION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Tiền bồi thường bảo hiểm con người, tài sản, trợ cấp tai nạn lao động được miễn thuế TNCN", "Miễn thuế bồi thường bảo hiểm, trợ cấp tai nạn lao động", true, null, "EXEMPTION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "TEXT" },
                    { "PIT_EXEMPTION_OVERTIME", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Phần tiền lương, tiền công trả cao hơn do làm việc ban đêm, làm thêm giờ được miễn thuế", "Miễn thuế phần làm đêm, thêm giờ", true, null, "EXEMPTION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "TEXT" },
                    { "PIT_EXEMPTION_RETIREMENT_PENSION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Tiền lương hưu do Quỹ bảo hiểm xã hội chi trả được miễn thuế TNCN", "Miễn thuế lương hưu từ Quỹ BHXH", true, null, "EXEMPTION", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "TEXT" },
                    { "PIT_RATE_NON_RESIDENT_SALARY", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "%", "Thuế suất thuế TNCN đối với thu nhập từ tiền lương, tiền công của cá nhân không cư trú", "Thuế suất tiền lương cá nhân không cư trú", true, null, "RATE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "RATE" },
                    { "PIT_SETTLEMENT_SELF_REQUIRED_IF_MED_EDU", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "Cờ xác định cá nhân có áp dụng giảm trừ y tế hoặc giáo dục thì bắt buộc phải tự quyết toán trực tiếp", "Có giảm trừ y tế, giáo dục thì phải tự quyết toán", true, null, "SETTLEMENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "FLAG" }
                });

            migrationBuilder.InsertData(
                table: "law_rule_definitions",
                columns: new[] { "rule_code", "created_at", "default_unit", "description", "display_name", "is_active", "required_for_flow3", "required_from_tax_year", "rule_group", "updated_at", "value_kind" },
                values: new object[] { "PIT_TAX_SCHEDULE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND/year", "Bậc thuế, ngưỡng thu nhập tính thuế và thuế suất từng bậc", "Biểu thuế lũy tiến từng phần (tiền lương, tiền công)", true, true, null, "SCHEDULE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SCHEDULE" });

            migrationBuilder.InsertData(
                table: "law_rule_definitions",
                columns: new[] { "rule_code", "created_at", "default_unit", "description", "display_name", "is_active", "required_from_tax_year", "rule_group", "updated_at", "value_kind" },
                values: new object[,]
                {
                    { "PIT_WITHHOLD_CASUAL_MIN_PAYMENT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND/payment", "Mức chi trả từ ngưỡng này trở lên mỗi lần thì phải khấu trừ thuế vãng lai", "Ngưỡng mỗi lần chi trả phải khấu trừ", true, null, "WITHHOLDING", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "AMOUNT" },
                    { "PIT_WITHHOLD_CASUAL_RATE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "%", "Tỷ lệ khấu trừ thuế TNCN tại nguồn đối với cá nhân không ký HĐLĐ hoặc HĐLĐ dưới 3 tháng", "Tỷ lệ khấu trừ lao động vãng lai", true, null, "WITHHOLDING", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "RATE" }
                });

            migrationBuilder.CreateIndex(
                name: "idx_law_change_ops_changeset",
                table: "law_change_ops",
                columns: new[] { "changeset_id", "decision" });

            migrationBuilder.CreateIndex(
                name: "IX_law_changeset_relations_changeset_id",
                table: "law_changeset_relations",
                column: "changeset_id");

            migrationBuilder.CreateIndex(
                name: "IX_law_changeset_relations_target_doc_id",
                table: "law_changeset_relations",
                column: "target_doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_law_changesets_document_id",
                table: "law_changesets",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "uq_law_changesets_ai_task_id",
                table: "law_changesets",
                column: "ai_task_id",
                unique: true,
                filter: "ai_task_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_law_orphan_resolutions_generated_op_id",
                table: "law_orphan_resolutions",
                column: "generated_op_id");

            migrationBuilder.CreateIndex(
                name: "IX_law_orphan_resolutions_version_id",
                table: "law_orphan_resolutions",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "uq_orphan_resolution_changeset_version",
                table: "law_orphan_resolutions",
                columns: new[] { "changeset_id", "version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_law_revisions_changeset_id",
                table: "law_revisions",
                column: "changeset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_law_revisions_document_id",
                table: "law_revisions",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "idx_law_rule_versions_active",
                table: "law_rule_versions",
                columns: new[] { "rule_code", "apply_from" },
                filter: "superseded_in_revision IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_law_rule_versions_created_in_revision",
                table: "law_rule_versions",
                column: "created_in_revision");

            migrationBuilder.CreateIndex(
                name: "IX_law_rule_versions_document_id",
                table: "law_rule_versions",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_law_rule_versions_source_op_id",
                table: "law_rule_versions",
                column: "source_op_id");

            migrationBuilder.CreateIndex(
                name: "IX_law_rule_versions_superseded_in_revision",
                table: "law_rule_versions",
                column: "superseded_in_revision");

            migrationBuilder.CreateIndex(
                name: "idx_legal_doc_rel_target",
                table: "legal_document_relations",
                column: "target_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_legal_document_relations_created_in_revision",
                table: "legal_document_relations",
                column: "created_in_revision");

            migrationBuilder.CreateIndex(
                name: "IX_legal_document_relations_source_document_id",
                table: "legal_document_relations",
                column: "source_document_id");

            migrationBuilder.CreateIndex(
                name: "uq_legal_documents_normalized",
                table: "legal_documents",
                column: "number_normalized",
                unique: true,
                filter: "number_normalized IS NOT NULL");

            // Exclusion constraint on law_rule_versions
            migrationBuilder.Sql(@"
                ALTER TABLE law_rule_versions 
                ADD CONSTRAINT law_rule_versions_no_overlap 
                EXCLUDE USING gist (
                    rule_code WITH =, 
                    daterange(apply_from, apply_to, '[)') WITH &&
                ) 
                WHERE (superseded_in_revision IS NULL) 
                DEFERRABLE INITIALLY DEFERRED;
            ");

            // Partial unique index for single open changeset
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX uq_law_changesets_single_open 
                ON law_changesets ((true)) 
                WHERE status IN ('EXTRACTING', 'READY', 'STALE');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS uq_law_changesets_single_open;");
            migrationBuilder.Sql("ALTER TABLE law_rule_versions DROP CONSTRAINT IF EXISTS law_rule_versions_no_overlap;");

            migrationBuilder.DropTable(
                name: "law_changeset_relations");

            migrationBuilder.DropTable(
                name: "law_orphan_resolutions");

            migrationBuilder.DropTable(
                name: "legal_document_relations");

            migrationBuilder.DropTable(
                name: "law_rule_versions");

            migrationBuilder.DropTable(
                name: "law_change_ops");

            migrationBuilder.DropTable(
                name: "law_revisions");

            migrationBuilder.DropTable(
                name: "law_rule_definitions");

            migrationBuilder.DropTable(
                name: "law_changesets");

            migrationBuilder.DropTable(
                name: "legal_documents");
        }
    }
}
