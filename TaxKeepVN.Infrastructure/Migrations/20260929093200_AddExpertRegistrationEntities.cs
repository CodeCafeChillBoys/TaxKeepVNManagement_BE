using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertRegistrationEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consultation_fee_configurations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    min_fee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    max_fee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultation_fee_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expert_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    job_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    company_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    bio = table.Column<string>(type: "text", nullable: true),
                    years_of_experience = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    current_position = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    experience_description = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    supplement_request_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expert_applications", x => x.id);
                    table.ForeignKey(
                        name: "FK_expert_applications_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_expert_applications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "specializations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_specializations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expert_application_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expert_application_audits", x => x.id);
                    table.ForeignKey(
                        name: "FK_expert_application_audits_expert_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "expert_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expert_application_audits_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expert_application_certificates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    certificate_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    certificate_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    certificate_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issuing_authority = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    has_expiry = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    file_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    file_mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    verification_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PendingVerification"),
                    verification_source = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    verification_note = table.Column<string>(type: "text", nullable: true),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expert_application_certificates", x => x.id);
                    table.ForeignKey(
                        name: "FK_expert_application_certificates_expert_applications_applica~",
                        column: x => x.application_id,
                        principalTable: "expert_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expert_application_certificates_users_verified_by",
                        column: x => x.verified_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "expert_application_fee_proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    proposed_fee = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expert_application_fee_proposals", x => x.id);
                    table.ForeignKey(
                        name: "FK_expert_application_fee_proposals_expert_applications_applic~",
                        column: x => x.application_id,
                        principalTable: "expert_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expert_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latest_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    company_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    bio = table.Column<string>(type: "text", nullable: true),
                    years_of_experience = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rating = table.Column<decimal>(type: "numeric(3,2)", nullable: false, defaultValue: 0m),
                    total_reviews = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expert_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_expert_profiles_expert_applications_latest_application_id",
                        column: x => x.latest_application_id,
                        principalTable: "expert_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expert_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expert_application_specializations",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialization_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expert_application_specializations", x => new { x.application_id, x.specialization_id });
                    table.ForeignKey(
                        name: "FK_expert_application_specializations_expert_applications_appl~",
                        column: x => x.application_id,
                        principalTable: "expert_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expert_application_specializations_specializations_speciali~",
                        column: x => x.specialization_id,
                        principalTable: "specializations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "consultation_fee_configurations",
                columns: new[] { "id", "duration_minutes", "is_active", "max_fee", "min_fee", "session_type" },
                values: new object[,]
                {
                    { 1, 30, true, 1000000m, 100000m, "ONLINE_MEETING" },
                    { 2, 60, true, 2000000m, 200000m, "ONLINE_MEETING" }
                });

            migrationBuilder.InsertData(
                table: "specializations",
                columns: new[] { "id", "code", "description", "display_order", "is_active", "name" },
                values: new object[,]
                {
                    { 1, "PIT", null, 1, true, "Thuế thu nhập cá nhân (TNCN)" },
                    { 2, "CIT", null, 2, true, "Thuế thu nhập doanh nghiệp (TNDN)" },
                    { 3, "FINALIZATION", null, 3, true, "Quyết toán thuế" },
                    { 4, "TAX_REFUND", null, 4, true, "Hoàn thuế" },
                    { 5, "INTERNAL_ACCOUNTING", null, 5, true, "Kế toán nội bộ" },
                    { 6, "TRANSFER_PRICING", null, 6, true, "Chuyển giá" },
                    { 7, "TAX_AGENT", null, 7, true, "Đại lý thuế" },
                    { 8, "CORPORATE_TAX_LEGAL", null, 8, true, "Pháp lý thuế doanh nghiệp" }
                });

            migrationBuilder.CreateIndex(
                name: "uq_consultation_fee_type_duration",
                table: "consultation_fee_configurations",
                columns: new[] { "session_type", "duration_minutes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_expert_audits_application_id",
                table: "expert_application_audits",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_expert_application_audits_actor_id",
                table: "expert_application_audits",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "idx_expert_certs_application_id",
                table: "expert_application_certificates",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "IX_expert_application_certificates_verified_by",
                table: "expert_application_certificates",
                column: "verified_by");

            migrationBuilder.CreateIndex(
                name: "uq_application_fee_proposal",
                table: "expert_application_fee_proposals",
                columns: new[] { "application_id", "session_type", "duration_minutes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expert_application_specializations_specialization_id",
                table: "expert_application_specializations",
                column: "specialization_id");

            migrationBuilder.CreateIndex(
                name: "idx_expert_applications_status",
                table: "expert_applications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_expert_applications_reviewed_by",
                table: "expert_applications",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "uq_expert_applications_number",
                table: "expert_applications",
                column: "application_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_user_active_pending_application",
                table: "expert_applications",
                column: "user_id",
                unique: true,
                filter: "status IN ('PendingReview', 'NeedSupplement')");

            migrationBuilder.CreateIndex(
                name: "IX_expert_profiles_latest_application_id",
                table: "expert_profiles",
                column: "latest_application_id");

            migrationBuilder.CreateIndex(
                name: "uq_expert_profiles_user_id",
                table: "expert_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_specializations_code",
                table: "specializations",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consultation_fee_configurations");

            migrationBuilder.DropTable(
                name: "expert_application_audits");

            migrationBuilder.DropTable(
                name: "expert_application_certificates");

            migrationBuilder.DropTable(
                name: "expert_application_fee_proposals");

            migrationBuilder.DropTable(
                name: "expert_application_specializations");

            migrationBuilder.DropTable(
                name: "expert_profiles");

            migrationBuilder.DropTable(
                name: "specializations");

            migrationBuilder.DropTable(
                name: "expert_applications");
        }
    }
}
