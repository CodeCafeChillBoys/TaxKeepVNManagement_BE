using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertCertificateConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expert_certificate_configurations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    default_issuing_authority = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    requires_expiry_date = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ai_keywords = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expert_certificate_configurations", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "expert_certificate_configurations",
                columns: new[] { "id", "ai_keywords", "code", "created_at", "default_issuing_authority", "description", "display_order", "is_active", "name", "updated_at" },
                values: new object[,]
                {
                    { 1, "đại lý thuế, tax agent, làm thủ tục về thuế, chứng chỉ hành nghề dịch vụ làm thủ tục về thuế", "TAX_AGENT", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tổng cục Thuế", "Chứng chỉ do Tổng cục Thuế cấp cho cá nhân đủ điều kiện hành nghề dịch vụ thuế.", 1, true, "Chứng chỉ hành nghề dịch vụ làm thủ tục về thuế (Đại lý thuế)", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 2, "kiểm toán viên, cpa, certified public accountant, bộ tài chính", "CPA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bộ Tài chính", "Chứng chỉ kiểm toán viên do Bộ Tài chính cấp.", 2, true, "Chứng chỉ Kiểm toán viên (CPA Việt Nam)", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 3, "hành nghề kế toán, kế toán viên, chứng chỉ kế toán", "ACCOUNTANT", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bộ Tài chính", "Chứng chỉ hành nghề kế toán do Bộ Tài chính cấp theo quy định Luật Kế toán.", 3, true, "Chứng chỉ hành nghề Kế toán", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                table: "expert_certificate_configurations",
                columns: new[] { "id", "ai_keywords", "code", "created_at", "default_issuing_authority", "description", "display_order", "is_active", "name", "requires_expiry_date", "updated_at" },
                values: new object[] { 4, "kế toán trưởng, bồi dưỡng kế toán trưởng", "CHIEF_ACCOUNTANT", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bộ Tài chính", "Chứng chỉ bồi dưỡng nghiệp vụ kế toán trưởng, có thời hạn hiệu lực 5 năm theo quy định.", 4, true, "Chứng chỉ bồi dưỡng Kế toán trưởng", true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "uq_expert_certificate_code",
                table: "expert_certificate_configurations",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expert_certificate_configurations");
        }
    }
}
