using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDescriptionToTaxDocumentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "document_types",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "code",
                keyValue: "SALES_INVOICE",
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "code",
                keyValue: "VAT_INVOICE",
                column: "description",
                value: null);

            migrationBuilder.UpdateData(
                table: "document_types",
                keyColumn: "code",
                keyValue: "WITHHOLDING_VOUCHER",
                column: "description",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description",
                table: "document_types");
        }
    }
}
