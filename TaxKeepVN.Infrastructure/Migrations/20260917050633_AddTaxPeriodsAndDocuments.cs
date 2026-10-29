using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxPeriodsAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_types",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_tax_eligible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_types", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "tax_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_year = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "DRAFT")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_periods", x => x.id);
                    table.ForeignKey(
                        name: "FK_tax_periods_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_url = table.Column<string>(type: "text", nullable: false),
                    original_filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    invoice_series = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: true),
                    seller_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    seller_tax_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    seller_address = table.Column<string>(type: "text", nullable: true),
                    seller_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    buyer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    buyer_tax_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    buyer_id_card = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    buyer_address = table.Column<string>(type: "text", nullable: true),
                    payment_method = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    total_amount_in_words = table.Column<string>(type: "text", nullable: true),
                    lookup_url = table.Column<string>(type: "text", nullable: true),
                    lookup_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    extracted_year = table.Column<short>(type: "smallint", nullable: true),
                    is_year_valid = table.Column<bool>(type: "boolean", nullable: true),
                    is_identity_valid = table.Column<bool>(type: "boolean", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "UPLOADED"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_documents_document_types_doc_type_code",
                        column: x => x.doc_type_code,
                        principalTable: "document_types",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_tax_periods_period_id",
                        column: x => x.period_id,
                        principalTable: "tax_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_order = table.Column<int>(type: "integer", nullable: false),
                    item_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    total_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_items_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "document_types",
                columns: new[] { "code", "is_tax_eligible", "name" },
                values: new object[,]
                {
                    { "SALES_INVOICE", true, "Hóa đơn bán hàng" },
                    { "VAT_INVOICE", true, "Hóa đơn GTGT" },
                    { "WITHHOLDING_VOUCHER", true, "Chứng từ khấu trừ thuế TNCN" }
                });

            migrationBuilder.CreateIndex(
                name: "idx_document_items_document_id",
                table: "document_items",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "idx_documents_doc_type_code",
                table: "documents",
                column: "doc_type_code");

            migrationBuilder.CreateIndex(
                name: "idx_documents_period_id",
                table: "documents",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "idx_documents_user_id",
                table: "documents",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_tax_periods_user_id",
                table: "tax_periods",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_tax_periods_user_year",
                table: "tax_periods",
                columns: new[] { "user_id", "tax_year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_items");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "document_types");

            migrationBuilder.DropTable(
                name: "tax_periods");
        }
    }
}
