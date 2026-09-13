using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDependentDocumentRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "date_of_birth",
                table: "dependents");

            migrationBuilder.AddColumn<decimal>(
                name: "tax_withheld",
                table: "income_sources",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "tax_year",
                table: "income_sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "total_income",
                table: "income_sources",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<bool>(
                name: "IsProfileComplete",
                table: "dependents",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "dependents",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.CreateTable(
                name: "dependent_document_rules",
                columns: table => new
                {
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_group = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    doc_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dependent_document_rules", x => x.rule_id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_group_doc_rule",
                table: "dependent_document_rules",
                columns: new[] { "target_group", "doc_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dependent_document_rules");

            migrationBuilder.DropColumn(
                name: "tax_withheld",
                table: "income_sources");

            migrationBuilder.DropColumn(
                name: "tax_year",
                table: "income_sources");

            migrationBuilder.DropColumn(
                name: "total_income",
                table: "income_sources");

            migrationBuilder.AlterColumn<bool>(
                name: "IsProfileComplete",
                table: "dependents",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "dependents",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_birth",
                table: "dependents",
                type: "date",
                nullable: true);
        }
    }
}
