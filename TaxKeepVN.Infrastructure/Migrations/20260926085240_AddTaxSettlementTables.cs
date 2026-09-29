using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxSettlementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    total_deductions = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    taxable_income_yearly = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    taxable_income_monthly = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_payable = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    due_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    applied_bracket_no = table.Column<int>(type: "integer", nullable: false),
                    pit_brackets_snapshot = table.Column<string>(type: "text", nullable: true),
                    deduction_config_snapshot = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "DRAFT"),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tax_settlement_income_items");

            migrationBuilder.DropTable(
                name: "tax_settlement_dossiers");
        }
    }
}
