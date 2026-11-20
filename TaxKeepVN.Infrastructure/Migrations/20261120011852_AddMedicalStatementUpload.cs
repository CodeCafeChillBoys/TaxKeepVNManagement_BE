using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicalStatementUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.Sql("""
                INSERT INTO document_types (code, name, description, is_tax_eligible)
                VALUES ('MEDICAL_COST_STATEMENT', 'Bảng kê chi phí khám bệnh, chữa bệnh',
                        'Bảng kê mẫu 01/KBCB hỗ trợ hóa đơn y tế.', FALSE)
                ON CONFLICT (code) DO NOTHING;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_documents_parent_document_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "idx_documents_parent_document_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "medical_statement_ocr",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "parent_document_id",
                table: "documents");

            migrationBuilder.Sql("UPDATE documents SET doc_type_code = NULL WHERE doc_type_code = 'MEDICAL_COST_STATEMENT';");

            migrationBuilder.Sql("DELETE FROM document_types WHERE code = 'MEDICAL_COST_STATEMENT';");
        }
    }
}
