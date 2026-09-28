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
            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "code",
                keyValue: "SALES_INVOICE");

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "code",
                keyValue: "VAT_INVOICE");

            migrationBuilder.DeleteData(
                table: "document_types",
                keyColumn: "code",
                keyValue: "WITHHOLDING_VOUCHER");

            migrationBuilder.CreateTable(
                name: "tax_settlement_dossiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    taxpayer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_year = table.Column<int>(type: "integer", nullable: false),
                    cutoff_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_gross_income = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_tax_withheld = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_insurance_deduction = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    personal_deduction_months = table.Column<int>(type: "integer", nullable: false),
                    personal_deduction_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    dependent_deduction_person_months = table.Column<int>(type: "integer", nullable: false),
                    dependent_deduction_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    charity_deduction = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    medical_deduction = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    education_deduction = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_deductions = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    taxable_income_yearly = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    taxable_income_monthly = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_payable = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    due_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    applied_bracket_no = table.Column<int>(type: "integer", nullable: false),
                    pit_brackets_snapshot = table.Column<string>(type: "text", nullable: true),
                    deduction_config_snapshot = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    zip_file_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    locked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_settlement_dossiers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_settlement_income_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dossier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    income_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    company_tax_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gross_income = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_withheld = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    insurance_deduction = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_selected = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_settlement_income_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_tax_settlement_income_items_tax_settlement_dossiers_dossier~",
                        column: x => x.dossier_id,
                        principalTable: "tax_settlement_dossiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_settlement_taxpayer_year",
                table: "tax_settlement_dossiers",
                columns: new[] { "taxpayer_id", "tax_year" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_settlement_income_items_dossier_id",
                table: "tax_settlement_income_items",
                column: "dossier_id");

            // Seed lại các loại chứng từ hiện có (đã bị delete bởi EF scaffold diff)
            // và thêm 2 loại mới: Biên lai y tế & Học phí giáo dục (Luật 109/2025/QH15 từ 2026)
            migrationBuilder.InsertData(
                table: "document_types",
                columns: new[] { "code", "is_tax_eligible", "name" },
                values: new object[,]
                {
                    { "SALES_INVOICE",    true, "Hóa đơn bán hàng" },
                    { "VAT_INVOICE",      true, "Hóa đơn GTGT" },
                    { "WITHHOLDING_VOUCHER", true, "Chứng từ khấu trừ thuế TNCN" },
                    { "MEDICAL_RECEIPT",  true, "Biên lai chi phí y tế (Giảm trừ từ 2026, tối đa 23tr/năm)" },
                    { "EDUCATION_RECEIPT",true, "Biên lai học phí / chi phí giáo dục (Giảm trừ từ 2026, tối đa 24tr/năm)" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tax_settlement_income_items");

            migrationBuilder.DropTable(
                name: "tax_settlement_dossiers");

            migrationBuilder.InsertData(
                table: "document_types",
                columns: new[] { "code", "is_tax_eligible", "name" },
                values: new object[,]
                {
                    { "SALES_INVOICE", true, "Hóa đơn bán hàng" },
                    { "VAT_INVOICE", true, "Hóa đơn GTGT" },
                    { "WITHHOLDING_VOUCHER", true, "Chứng từ khấu trừ thuế TNCN" }
                });
        }
    }
}
