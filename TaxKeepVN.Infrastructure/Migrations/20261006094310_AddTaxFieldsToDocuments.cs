using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxFieldsToDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "insurance_deducted",
                table: "documents",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_withheld",
                table: "documents",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_income",
                table: "documents",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "insurance_deducted",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "tax_withheld",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "total_income",
                table: "documents");
        }
    }
}
