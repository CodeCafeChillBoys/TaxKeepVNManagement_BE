using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicalEducationDeductionsAndDocumentTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thêm 2 cột mới cho giảm trừ chi phí y tế và giáo dục theo Luật thuế TNCN mới (từ 2026)
            migrationBuilder.AddColumn<decimal>(
                name: "medical_deduction",
                table: "tax_settlement_dossiers",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "education_deduction",
                table: "tax_settlement_dossiers",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Seed thêm 2 loại chứng từ mới: Biên lai y tế & Học phí giáo dục (Idempotent)
            migrationBuilder.Sql(@"
                INSERT INTO document_types (code, is_tax_eligible, name)
                VALUES 
                    ('MEDICAL_RECEIPT', TRUE, 'Biên lai chi phí y tế (Giảm trừ từ 2026, tối đa 23tr/năm)'),
                    ('EDUCATION_RECEIPT', TRUE, 'Biên lai học phí / chi phí giáo dục (Giảm trừ từ 2026, tối đa 24tr/năm)')
                ON CONFLICT (code) DO UPDATE 
                SET name = EXCLUDED.name, is_tax_eligible = EXCLUDED.is_tax_eligible;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "medical_deduction",
                table: "tax_settlement_dossiers");

            migrationBuilder.DropColumn(
                name: "education_deduction",
                table: "tax_settlement_dossiers");

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "code",
                keyValue: "MEDICAL_RECEIPT");

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "code",
                keyValue: "EDUCATION_RECEIPT");
        }
    }
}
