using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxKeepVN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowDependentDocumentRuleAlternativeGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_group_doc_rule",
                table: "dependent_document_rules");

            migrationBuilder.CreateIndex(
                name: "uq_group_doc_rule_with_alternative_group",
                table: "dependent_document_rules",
                columns: new[] { "target_group", "doc_type", "alternative_group" },
                unique: true,
                filter: "\"alternative_group\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_group_doc_rule_without_alternative_group",
                table: "dependent_document_rules",
                columns: new[] { "target_group", "doc_type" },
                unique: true,
                filter: "\"alternative_group\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_group_doc_rule_with_alternative_group",
                table: "dependent_document_rules");

            migrationBuilder.DropIndex(
                name: "uq_group_doc_rule_without_alternative_group",
                table: "dependent_document_rules");

            migrationBuilder.CreateIndex(
                name: "uq_group_doc_rule",
                table: "dependent_document_rules",
                columns: new[] { "target_group", "doc_type" },
                unique: true);
        }
    }
}
